#!/usr/bin/env bash
# Backup notturno del database GestiSoftGestionale: backup fisico completo pgBackRest (repo
# locale, WAL già archiviato in continuo da Postgres) + pg_dump logico indipendente (seconda rete
# di sicurezza, retention breve). Porting bash di backup-nightly.ps1 (stessa identica logica) per
# la VPS di produzione — da eseguire una volta a notte via cron. Vedi docs/backup-restore.md.
set -uo pipefail

CONTAINER_NAME="gestisoft-gestionale-postgres"
STANZA="gestisoft"
PG_USER="gestisoft"
PG_DB="gestisoft"
DUMP_RETENTION_DAYS=7

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
LOG_DIR="$REPO_ROOT/docker/postgres/logs"
DUMP_DIR="$REPO_ROOT/docker/postgres/dumps"
mkdir -p "$LOG_DIR" "$DUMP_DIR"

TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
LOG_FILE="$LOG_DIR/backup-nightly_$TIMESTAMP.log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# Riga in log_eventi (stessa tabella già usata da tutta l'app, categoria "Backup") — così lo
# storico dei backup notturni compare nella pagina Backup del pannello Super Admin.
log_evento() {
    local livello="$1"
    local messaggio="$2"
    local escaped="${messaggio//\'/\'\'}"
    local sql="INSERT INTO log_eventi (\"Id\",\"Livello\",\"Messaggio\",\"Origine\",\"Categoria\",\"CreatedAtUtc\") VALUES (gen_random_uuid(), $livello, '$escaped', 'BackupScript', 'Backup', now());"
    echo "$sql" | docker exec -i -u postgres "$CONTAINER_NAME" psql -U "$PG_USER" -d "$PG_DB" >/dev/null 2>&1
}

had_error=0
riepilogo_pgbackrest="non eseguito"
riepilogo_pgdump="non eseguito"

log "=== Backup notturno avviato ==="

# --- 1. Backup fisico completo pgBackRest (+ WAL già archiviato in continuo) ---
if pgbackrest_output=$(docker exec -u postgres "$CONTAINER_NAME" pgbackrest --stanza="$STANZA" --type=full backup 2>&1); then
    log "pgBackRest: backup completato con successo."
    riepilogo_pgbackrest="OK"
else
    log "ERRORE pgBackRest: $pgbackrest_output"
    riepilogo_pgbackrest="ERRORE ($pgbackrest_output)"
    had_error=1
fi

# --- 2. pg_dump logico indipendente (rete di sicurezza separata dal repo pgBackRest) ---
# "//tmp/..." (doppio slash) invece di "/tmp/...": innocuo su Linux (POSIX normalizza i due
# percorsi allo stesso modo), ma evita che Git Bash su Windows lo traduca per errore in un
# percorso locale durante i test in sviluppo — nessuna differenza di comportamento sulla VPS.
container_dump_path="//tmp/gestisoft_$TIMESTAMP.dump"
local_dump_file="$DUMP_DIR/gestisoft_$TIMESTAMP.dump"
if pgdump_output=$(docker exec -u postgres "$CONTAINER_NAME" pg_dump -U "$PG_USER" -d "$PG_DB" -Fc -f "$container_dump_path" 2>&1); then
    if docker cp "$CONTAINER_NAME:$container_dump_path" "$local_dump_file" 2>&1; then
        docker exec -u postgres "$CONTAINER_NAME" rm "$container_dump_path" >/dev/null 2>&1
        size=$(stat -c%s "$local_dump_file" 2>/dev/null)
        log "pg_dump: completato, $local_dump_file ($size byte)."
        riepilogo_pgdump="OK ($size byte)"
    else
        log "ERRORE pg_dump: docker cp del dump fallito"
        riepilogo_pgdump="ERRORE (docker cp fallito)"
        had_error=1
    fi
else
    log "ERRORE pg_dump: $pgdump_output"
    riepilogo_pgdump="ERRORE ($pgdump_output)"
    had_error=1
fi

# --- 3. Retention pg_dump (i backup pgBackRest gestiscono la propria retention da soli) ---
find "$DUMP_DIR" -maxdepth 1 -name 'gestisoft_*.dump' -mtime "+$DUMP_RETENTION_DAYS" -print -delete 2>/dev/null | while read -r f; do
    log "Retention: rimosso dump vecchio $(basename "$f")."
done

if [ "$had_error" -eq 1 ]; then
    log "=== Backup notturno TERMINATO CON ERRORI — controllare sopra ==="
    log_evento 3 "Backup notturno con errori — pgBackRest: $riepilogo_pgbackrest; pg_dump: $riepilogo_pgdump."
    exit 1
else
    log "=== Backup notturno completato con successo ==="
    log_evento 1 "Backup notturno completato — pgBackRest: $riepilogo_pgbackrest; pg_dump: $riepilogo_pgdump."
    exit 0
fi
