<#
.SYNOPSIS
    Backup notturno del database GestiSoftGestionale: backup fisico completo pgBackRest
    (repo locale, WAL già archiviato in continuo da Postgres) + pg_dump logico indipendente
    (seconda rete di sicurezza, retention breve).

.DESCRIPTION
    Da eseguire una volta a notte via Windows Task Scheduler (o cron, su una futura VPS Linux).
    Non tocca mai il volume dati reale: pgBackRest legge PGDATA in sola lettura per il backup,
    pg_dump si limita a interrogare il database via connessione normale.
    Vedi docs/backup-restore.md per il runbook completo.
#>

$ErrorActionPreference = 'Stop'
# A livello di script, non dentro la funzione: Windows PowerShell 5.1 non applica in modo
# affidabile $OutputEncoding a un pipe verso un processo nativo se assegnato solo nello scope
# locale di una funzione (verificato — un trattino lungo/lettera accentata arriva corrotto nel
# database se questa riga si trova dentro Write-LogEvento invece che qui).
$OutputEncoding = New-Object System.Text.UTF8Encoding($false)

$ContainerName = 'gestisoft-gestionale-postgres'
$Stanza = 'gestisoft'
$PgUser = 'gestisoft'
$PgDb = 'gestisoft'
$DumpRetentionDays = 7

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptDir '..\..\..')
$LogDir = Join-Path $RepoRoot 'docker\postgres\logs'
$DumpDir = Join-Path $RepoRoot 'docker\postgres\dumps'

foreach ($dir in @($LogDir, $DumpDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

$Timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$LogFile = Join-Path $LogDir "backup-nightly_$Timestamp.log"

function Write-Log {
    param([string]$Message)
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
    Write-Output $line
    Add-Content -Path $LogFile -Value $line
}

# Riga in log_eventi (stessa tabella già usata da tutta l'app, categoria "Backup") — così lo
# storico dei backup notturni compare nella pagina Backup del pannello Super Admin (e nella
# pagina Log generale), non solo nei file .log su disco che nessuno guarda finché non serve.
function Write-LogEvento {
    param([int]$Livello, [string]$Messaggio)
    # SQL passata via stdin (non come argomento -c) apposta: un argomento con virgolette doppie
    # incorporate (necessarie per i nomi colonna, definiti PascalCase da EF) viene rimaneggiato in
    # modo imprevedibile da PowerShell quando passato a un eseguibile nativo come docker.exe — via
    # stdin il problema non si pone, il testo arriva intatto.
    $escaped = $Messaggio.Replace("'", "''")
    $sql = 'INSERT INTO log_eventi ("Id","Livello","Messaggio","Origine","Categoria","CreatedAtUtc") VALUES (gen_random_uuid(), ' + $Livello + ", '" + $escaped + "', 'BackupScript', 'Backup', now());"
    $sql | docker exec -i -u postgres $ContainerName psql -U $PgUser -d $PgDb 2>&1 | Out-Null
}

$hadError = $false
$riepilogoPgBackRest = 'non eseguito'
$riepilogoPgDump = 'non eseguito'

Write-Log "=== Backup notturno avviato ==="

# --- 1. Backup fisico completo pgBackRest (+ WAL già archiviato in continuo) ---
try {
    Write-Log "pgBackRest: avvio backup full (stanza=$Stanza)..."
    docker exec -u postgres $ContainerName pgbackrest --stanza=$Stanza --type=full backup 2>&1 |
        ForEach-Object { Write-Log "  pgbackrest: $_" }
    if ($LASTEXITCODE -ne 0) { throw "pgbackrest backup ha restituito exit code $LASTEXITCODE" }
    Write-Log "pgBackRest: backup completato con successo."
    $riepilogoPgBackRest = 'OK'
}
catch {
    Write-Log "ERRORE pgBackRest: $($_.Exception.Message)"
    $riepilogoPgBackRest = "ERRORE ($($_.Exception.Message))"
    $hadError = $true
}

# --- 2. pg_dump logico indipendente (rete di sicurezza separata dal repo pgBackRest) ---
try {
    Write-Log "pg_dump: avvio dump logico..."
    $containerDumpPath = "/tmp/gestisoft_$Timestamp.dump"
    docker exec -u postgres $ContainerName pg_dump -U $PgUser -d $PgDb -Fc -f $containerDumpPath 2>&1 |
        ForEach-Object { Write-Log "  pg_dump: $_" }
    if ($LASTEXITCODE -ne 0) { throw "pg_dump ha restituito exit code $LASTEXITCODE" }

    $localDumpFile = Join-Path $DumpDir "gestisoft_$Timestamp.dump"
    docker cp "${ContainerName}:${containerDumpPath}" $localDumpFile
    if ($LASTEXITCODE -ne 0) { throw "docker cp del dump ha restituito exit code $LASTEXITCODE" }

    docker exec -u postgres $ContainerName rm $containerDumpPath | Out-Null

    $size = (Get-Item $localDumpFile).Length
    Write-Log "pg_dump: completato, $localDumpFile ($size byte)."
    $riepilogoPgDump = "OK ($size byte)"
}
catch {
    Write-Log "ERRORE pg_dump: $($_.Exception.Message)"
    $riepilogoPgDump = "ERRORE ($($_.Exception.Message))"
    $hadError = $true
}

# --- 3. Retention pg_dump (i backup pgBackRest gestiscono la propria retention da soli) ---
try {
    $cutoff = (Get-Date).AddDays(-$DumpRetentionDays)
    $old = Get-ChildItem -Path $DumpDir -Filter 'gestisoft_*.dump' | Where-Object { $_.LastWriteTime -lt $cutoff }
    foreach ($f in $old) {
        Remove-Item $f.FullName -Force
        Write-Log "Retention: rimosso dump vecchio $($f.Name)."
    }
}
catch {
    Write-Log "ATTENZIONE: pulizia retention pg_dump fallita: $($_.Exception.Message)"
}

if ($hadError) {
    Write-Log "=== Backup notturno TERMINATO CON ERRORI — controllare sopra ==="
    Write-LogEvento -Livello 3 -Messaggio "Backup notturno con errori — pgBackRest: $riepilogoPgBackRest; pg_dump: $riepilogoPgDump."
    exit 1
}
else {
    Write-Log "=== Backup notturno completato con successo ==="
    Write-LogEvento -Livello 1 -Messaggio "Backup notturno completato — pgBackRest: $riepilogoPgBackRest; pg_dump: $riepilogoPgDump."
    exit 0
}
