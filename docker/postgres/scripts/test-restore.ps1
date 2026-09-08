<#
.SYNOPSIS
    Test di restore reale del backup pgBackRest, su un container/volume usa-e-getta. Da eseguire
    una volta al mese (Task Scheduler) — un backup mai testato potrebbe non essere davvero
    ripristinabile (formato corrotto, catena WAL incompleta, config cambiata) e lo scopriresti
    solo nel momento peggiore possibile: durante un vero disastro.

.DESCRIPTION
    Non tocca MAI il volume dati reale (gestisoft_postgres_data) né il container reale: crea un
    volume dati nuovo con nome univoco a timestamp, un container temporaneo con la stessa
    immagine, ripristina l'ultimo backup pgBackRest lì dentro (volume repo montato in sola
    lettura), verifica che il database ripristinato risponda e che i conteggi di alcune tabelle
    chiave combacino con quelli del database live, poi distrugge tutto — anche se qualcosa va
    storto a metà (blocco finally). Vedi docs/backup-restore.md.
#>

$ErrorActionPreference = 'Stop'
# A livello di script, non dentro la funzione: vedi commento identico in backup-nightly.ps1
# (Windows PowerShell 5.1 non applica $OutputEncoding in modo affidabile se assegnato solo nello
# scope locale di una funzione).
$OutputEncoding = New-Object System.Text.UTF8Encoding($false)

$Image = 'gestisoftgestionale-postgres'
$RepoVolume = 'gestisoftgestionale_gestisoft_pgbackrest_repo'
$LiveContainer = 'gestisoft-gestionale-postgres'
$Stanza = 'gestisoft'
$PgUser = 'gestisoft'
$PgDb = 'gestisoft'
$TablesToCompare = @('prenotazioni', 'ospiti')

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptDir '..\..\..')
$LogDir = Join-Path $RepoRoot 'docker\postgres\logs'
if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }

$Timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$LogFile = Join-Path $LogDir "test-restore_$Timestamp.log"

function Write-Log {
    param([string]$Message)
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
    Write-Output $line
    Add-Content -Path $LogFile -Value $line
}

# Sempre sul container LIVE (quello temporaneo di restore non esiste più a fine test) — stessa
# tabella log_eventi già usata da tutta l'app, categoria "Backup", visibile nella pagina Backup.
function Write-LogEvento {
    param([int]$Livello, [string]$Messaggio)
    # SQL via stdin, non come argomento -c: vedi stesso commento in backup-nightly.ps1 (PowerShell
    # rimaneggia le virgolette doppie incorporate quando passate come argomento a un eseguibile nativo).
    $escaped = $Messaggio.Replace("'", "''")
    $sql = 'INSERT INTO log_eventi ("Id","Livello","Messaggio","Origine","Categoria","CreatedAtUtc") VALUES (gen_random_uuid(), ' + $Livello + ", '" + $escaped + "', 'BackupScript', 'Backup', now());"
    $sql | docker exec -i -u postgres $LiveContainer psql -U $PgUser -d $PgDb 2>&1 | Out-Null
}

$DataVolume = "gestisoft_test_restore_data_$Timestamp"
$RestoreContainer = "gestisoft-test-restore-$Timestamp"
$success = $false
$erroreMessaggio = $null

try {
    Write-Log "=== Test di restore avviato ($Timestamp) ==="

    Write-Log "Creo volume dati temporaneo: $DataVolume"
    docker volume create $DataVolume | Out-Null

    Write-Log "Avvio container temporaneo per il restore (entrypoint disattivato, solo pgbackrest)..."
    docker run -d --name $RestoreContainer `
        -v "${DataVolume}:/var/lib/postgresql/data" `
        -v "${RepoVolume}:/var/lib/pgbackrest:ro" `
        -e POSTGRES_PASSWORD=test_restore_only `
        --entrypoint sleep `
        $Image infinity | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "avvio container di restore fallito" }
    Start-Sleep -Seconds 2

    Write-Log "pgbackrest restore in corso..."
    docker exec -u postgres $RestoreContainer pgbackrest --stanza=$Stanza restore 2>&1 |
        ForEach-Object { Write-Log "  pgbackrest: $_" }
    if ($LASTEXITCODE -ne 0) { throw "pgbackrest restore ha restituito exit code $LASTEXITCODE" }

    Write-Log "Rimuovo il container 'sleep' e avvio Postgres sui dati ripristinati (repo ancora montato, serve per il replay WAL)..."
    docker stop $RestoreContainer | Out-Null
    docker rm $RestoreContainer | Out-Null
    docker run -d --name $RestoreContainer `
        -v "${DataVolume}:/var/lib/postgresql/data" `
        -v "${RepoVolume}:/var/lib/pgbackrest:ro" `
        -e POSTGRES_PASSWORD=test_restore_only `
        $Image | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "avvio Postgres sui dati ripristinati fallito" }

    Write-Log "Attendo che Postgres completi il recovery..."
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        docker exec -u postgres $RestoreContainer pg_isready -U $PgUser -d $PgDb 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    }
    if (-not $ready) { throw "Postgres non è diventato pronto entro il timeout — vedi 'docker logs $RestoreContainer'" }
    Write-Log "Postgres ripristinato è pronto."

    $allMatch = $true
    foreach ($table in $TablesToCompare) {
        $restoredCount = (docker exec -u postgres $RestoreContainer psql -U $PgUser -d $PgDb -t -A -c "SELECT count(*) FROM $table;").Trim()
        $liveCount = (docker exec $LiveContainer psql -U $PgUser -d $PgDb -t -A -c "SELECT count(*) FROM $table;").Trim()
        if ($restoredCount -eq $liveCount) {
            Write-Log "OK: $table — ripristinato=$restoredCount, live=$liveCount (combaciano)."
        }
        else {
            Write-Log "ATTENZIONE: $table — ripristinato=$restoredCount, live=$liveCount (NON combaciano — normale solo se ci sono state scritture reali dopo l'ultimo backup)."
            $allMatch = $false
        }
    }

    $success = $allMatch
}
catch {
    Write-Log "ERRORE: $($_.Exception.Message)"
    $erroreMessaggio = $_.Exception.Message
    $success = $false
}
finally {
    Write-Log "Pulizia: rimuovo container e volume temporanei..."
    docker stop $RestoreContainer 2>&1 | Out-Null
    docker rm $RestoreContainer 2>&1 | Out-Null
    docker volume rm $DataVolume 2>&1 | Out-Null
    Write-Log "Pulizia completata."
}

if ($success) {
    Write-Log "=== Test di restore RIUSCITO ==="
    Write-LogEvento -Livello 1 -Messaggio "Test di restore mensile riuscito — conteggi combacianti su $($TablesToCompare -join ', ')."
    exit 0
}
else {
    Write-Log "=== Test di restore FALLITO O CON DISCREPANZE — controllare sopra ==="
    $dettaglio = if ($erroreMessaggio) { $erroreMessaggio } else { 'discrepanze nei conteggi, vedi log per il dettaglio' }
    Write-LogEvento -Livello 3 -Messaggio "Test di restore mensile fallito — $dettaglio."
    exit 1
}
