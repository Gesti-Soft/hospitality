#!/usr/bin/env bash
# Controllo di salute del WAL archiving continuo (pg_stat_archiver). Porting bash di
# check-archiver.ps1 (stessa identica logica) per la VPS di produzione — da eseguire spesso
# (ogni ora via cron). Se archive_command fallisce silenziosamente (repo pieno, permessi, bug di
# config), Postgres accumula WAL fino a riempire il disco e può bloccarsi: non è un rischio "manca
# il backup", è un rischio di disponibilità del database live. Non blocca né modifica nulla: legge
# solo pg_stat_archiver. Vedi docs/backup-restore.md.
set -uo pipefail

CONTAINER_NAME="gestisoft-gestionale-postgres"
PG_USER="gestisoft"
PG_DB="gestisoft"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
LOG_DIR="$REPO_ROOT/docker/postgres/logs"
mkdir -p "$LOG_DIR"
LOG_FILE="$LOG_DIR/check-archiver.log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# Solo su problema reale (mai ad ogni giro orario "OK") — stessa tabella log_eventi già usata da
# tutta l'app, categoria "Backup", visibile nella pagina Backup del pannello Super Admin.
log_evento() {
    local livello="$1"
    local messaggio="$2"
    local escaped="${messaggio//\'/\'\'}"
    local sql="INSERT INTO log_eventi (\"Id\",\"Livello\",\"Messaggio\",\"Origine\",\"Categoria\",\"CreatedAtUtc\") VALUES (gen_random_uuid(), $livello, '$escaped', 'BackupScript', 'Backup', now());"
    echo "$sql" | docker exec -i -u postgres "$CONTAINER_NAME" psql -U "$PG_USER" -d "$PG_DB" >/dev/null 2>&1
}

sql="SELECT coalesce(last_archived_wal,''), coalesce(last_archived_time::text,''), coalesce(last_failed_wal,''), coalesce(last_failed_time::text,''), failed_count FROM pg_stat_archiver;"

raw=$(docker exec -u postgres "$CONTAINER_NAME" psql -U "$PG_USER" -d "$PG_DB" -t -A -F '|' -c "$sql" 2>&1)
if [ $? -ne 0 ]; then
    log "ERRORE eseguendo il controllo: $raw"
    log_evento 3 "Controllo archiviazione WAL non eseguito: $raw"
    exit 2
fi

IFS='|' read -r last_archived_wal last_archived_time last_failed_wal last_failed_time failed_count <<< "$raw"

failed_more_recent=0
if [ -n "$last_failed_wal" ]; then
    if [ -z "$last_archived_time" ]; then
        failed_more_recent=1
    else
        failed_epoch=$(date -d "$last_failed_time" +%s 2>/dev/null || echo 0)
        archived_epoch=$(date -d "$last_archived_time" +%s 2>/dev/null || echo 0)
        if [ "$failed_epoch" -gt "$archived_epoch" ]; then
            failed_more_recent=1
        fi
    fi
fi

if [ "$failed_more_recent" -eq 1 ]; then
    messaggio="Controllo archiviazione WAL: ultimo tentativo FALLITO ($last_failed_wal alle $last_failed_time, failed_count=$failed_count) e non ancora seguito da un successo. Controllare lo spazio libero del volume pgbackrest_repo."
    log "WARNING: $messaggio"
    log_evento 2 "$messaggio"
    exit 1
else
    log "OK: ultimo WAL archiviato con successo: $last_archived_wal alle $last_archived_time (failed_count totale=$failed_count)."
    exit 0
fi
