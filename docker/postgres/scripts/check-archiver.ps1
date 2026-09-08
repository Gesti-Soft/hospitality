<#
.SYNOPSIS
    Controllo di salute del WAL archiving continuo (pg_stat_archiver). Da eseguire spesso
    (es. ogni ora via Windows Task Scheduler) — se archive_command fallisce silenziosamente
    (repo pieno, permessi, bug di config), Postgres accumula WAL fino a riempire il disco e può
    bloccarsi: non è un rischio "manca il backup", è un rischio di disponibilità del database live.

.DESCRIPTION
    Non blocca né modifica nulla: legge solo pg_stat_archiver e scrive un log, WARNING se
    l'ultimo tentativo di archiviazione registrato è fallito e non è ancora stato seguito da un
    successo. Vedi docs/backup-restore.md.
#>

$ErrorActionPreference = 'Stop'
# A livello di script, non dentro la funzione: vedi commento identico in backup-nightly.ps1
# (Windows PowerShell 5.1 non applica $OutputEncoding in modo affidabile se assegnato solo nello
# scope locale di una funzione).
$OutputEncoding = New-Object System.Text.UTF8Encoding($false)

$ContainerName = 'gestisoft-gestionale-postgres'
$PgUser = 'gestisoft'
$PgDb = 'gestisoft'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptDir '..\..\..')
$LogDir = Join-Path $RepoRoot 'docker\postgres\logs'
if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
$LogFile = Join-Path $LogDir 'check-archiver.log'

function Write-Log {
    param([string]$Message)
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
    Write-Output $line
    Add-Content -Path $LogFile -Value $line
}

# Solo su problema reale (mai ad ogni giro orario "OK") — in log_eventi, categoria "Backup",
# stessa tabella già usata da tutta l'app, così un'archiviazione bloccata compare nella pagina
# Backup del pannello Super Admin, non solo nel file .log locale.
function Write-LogEvento {
    param([int]$Livello, [string]$Messaggio)
    # SQL via stdin, non come argomento -c: vedi stesso commento in backup-nightly.ps1 (PowerShell
    # rimaneggia le virgolette doppie incorporate quando passate come argomento a un eseguibile nativo).
    $escaped = $Messaggio.Replace("'", "''")
    $sql = 'INSERT INTO log_eventi ("Id","Livello","Messaggio","Origine","Categoria","CreatedAtUtc") VALUES (gen_random_uuid(), ' + $Livello + ", '" + $escaped + "', 'BackupScript', 'Backup', now());"
    $sql | docker exec -i -u postgres $ContainerName psql -U $PgUser -d $PgDb 2>&1 | Out-Null
}

$sql ="SELECT coalesce(last_archived_wal,''), coalesce(last_archived_time::text,''), coalesce(last_failed_wal,''), coalesce(last_failed_time::text,''), failed_count FROM pg_stat_archiver;"

try {
    $raw = docker exec -u postgres $ContainerName psql -U $PgUser -d $PgDb -t -A -F '|' -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw "psql ha restituito exit code $LASTEXITCODE : $raw" }

    $fields = $raw.Trim() -split '\|'
    $lastArchivedWal = $fields[0]
    $lastArchivedTime = $fields[1]
    $lastFailedWal = $fields[2]
    $lastFailedTime = $fields[3]
    $failedCount = $fields[4]

    $failedMoreRecent = $false
    if ($lastFailedWal -ne '') {
        if ($lastArchivedTime -eq '') {
            $failedMoreRecent = $true
        }
        else {
            $failedMoreRecent = ([datetime]$lastFailedTime) -gt ([datetime]$lastArchivedTime)
        }
    }

    if ($failedMoreRecent) {
        $messaggio = "Controllo archiviazione WAL: ultimo tentativo FALLITO ($lastFailedWal alle $lastFailedTime, failed_count=$failedCount) e non ancora seguito da un successo. Controllare lo spazio libero del volume pgbackrest_repo."
        Write-Log "WARNING: $messaggio"
        Write-LogEvento -Livello 2 -Messaggio $messaggio
        exit 1
    }
    else {
        Write-Log "OK: ultimo WAL archiviato con successo: $lastArchivedWal alle $lastArchivedTime (failed_count totale=$failedCount)."
        exit 0
    }
}
catch {
    Write-Log "ERRORE eseguendo il controllo: $($_.Exception.Message)"
    Write-LogEvento -Livello 3 -Messaggio "Controllo archiviazione WAL non eseguito: $($_.Exception.Message)"
    exit 2
}
