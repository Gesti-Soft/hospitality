#!/usr/bin/env bash
# Test di restore reale del backup pgBackRest, su un container/volume usa-e-getta. Porting bash
# di test-restore.ps1 (stessa identica logica) per la VPS di produzione — da eseguire una volta al
# mese via cron. Non tocca MAI il volume dati reale né il container reale: crea un volume dati
# nuovo con nome univoco a timestamp, ripristina l'ultimo backup lì dentro (volume repo montato in
# sola lettura), verifica i conteggi contro il database live, poi distrugge tutto — anche se
# qualcosa va storto a metà (trap EXIT, equivalente del blocco finally). Vedi docs/backup-restore.md.
set -uo pipefail

IMAGE="gestisoftgestionale-postgres"
# Nome del volume repo derivato dal nome cartella del progetto ("gestisoftgestionale", prefisso
# Docker Compose) — se sulla VPS il repo è clonato con un nome diverso, verificare con
# `docker volume ls | grep pgbackrest_repo` e correggere questa riga di conseguenza.
REPO_VOLUME="gestisoftgestionale_gestisoft_pgbackrest_repo"
LIVE_CONTAINER="gestisoft-gestionale-postgres"
STANZA="gestisoft"
PG_USER="gestisoft"
PG_DB="gestisoft"
TABLES_TO_COMPARE=("prenotazioni" "ospiti")

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
LOG_DIR="$REPO_ROOT/docker/postgres/logs"
mkdir -p "$LOG_DIR"

TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
LOG_FILE="$LOG_DIR/test-restore_$TIMESTAMP.log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# Sempre sul container LIVE (quello temporaneo di restore non esiste più a fine test).
log_evento() {
    local livello="$1"
    local messaggio="$2"
    local escaped="${messaggio//\'/\'\'}"
    local sql="INSERT INTO log_eventi (\"Id\",\"Livello\",\"Messaggio\",\"Origine\",\"Categoria\",\"CreatedAtUtc\") VALUES (gen_random_uuid(), $livello, '$escaped', 'BackupScript', 'Backup', now());"
    echo "$sql" | docker exec -i -u postgres "$LIVE_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" >/dev/null 2>&1
}

DATA_VOLUME="gestisoft_test_restore_data_$TIMESTAMP"
RESTORE_CONTAINER="gestisoft-test-restore-$TIMESTAMP"
success=0
errore_messaggio=""

cleanup() {
    log "Pulizia: rimuovo container e volume temporanei..."
    docker stop "$RESTORE_CONTAINER" >/dev/null 2>&1
    docker rm "$RESTORE_CONTAINER" >/dev/null 2>&1
    docker volume rm "$DATA_VOLUME" >/dev/null 2>&1
    log "Pulizia completata."

    if [ "$success" -eq 1 ]; then
        log "=== Test di restore RIUSCITO ==="
        joined=$(printf ', %s' "${TABLES_TO_COMPARE[@]}")
        joined="${joined#, }"
        log_evento 1 "Test di restore mensile riuscito — conteggi combacianti su $joined."
        exit 0
    else
        log "=== Test di restore FALLITO O CON DISCREPANZE — controllare sopra ==="
        dettaglio="${errore_messaggio:-discrepanze nei conteggi, vedi log per il dettaglio}"
        log_evento 3 "Test di restore mensile fallito — $dettaglio."
        exit 1
    fi
}
trap cleanup EXIT

log "=== Test di restore avviato ($TIMESTAMP) ==="

log "Creo volume dati temporaneo: $DATA_VOLUME"
if ! docker volume create "$DATA_VOLUME" >/dev/null; then
    errore_messaggio="creazione volume dati temporaneo fallita"
    log "ERRORE: $errore_messaggio"
    exit 1
fi

log "Avvio container temporaneo per il restore (entrypoint disattivato, solo pgbackrest)..."
if ! docker run -d --name "$RESTORE_CONTAINER" \
    -v "$DATA_VOLUME:/var/lib/postgresql/data" \
    -v "$REPO_VOLUME:/var/lib/pgbackrest:ro" \
    -e POSTGRES_PASSWORD=test_restore_only \
    --entrypoint sleep \
    "$IMAGE" infinity >/dev/null; then
    errore_messaggio="avvio container di restore fallito"
    log "ERRORE: $errore_messaggio"
    exit 1
fi
sleep 2

log "pgbackrest restore in corso..."
if ! restore_output=$(docker exec -u postgres "$RESTORE_CONTAINER" pgbackrest --stanza="$STANZA" restore 2>&1); then
    errore_messaggio="pgbackrest restore fallito: $restore_output"
    log "ERRORE: $errore_messaggio"
    exit 1
fi
[ -n "$restore_output" ] && log "  pgbackrest: $restore_output"

log "Rimuovo il container 'sleep' e avvio Postgres sui dati ripristinati (repo ancora montato, serve per il replay WAL)..."
docker stop "$RESTORE_CONTAINER" >/dev/null 2>&1
docker rm "$RESTORE_CONTAINER" >/dev/null 2>&1
if ! docker run -d --name "$RESTORE_CONTAINER" \
    -v "$DATA_VOLUME:/var/lib/postgresql/data" \
    -v "$REPO_VOLUME:/var/lib/pgbackrest:ro" \
    -e POSTGRES_PASSWORD=test_restore_only \
    "$IMAGE" >/dev/null; then
    errore_messaggio="avvio Postgres sui dati ripristinati fallito"
    log "ERRORE: $errore_messaggio"
    exit 1
fi

log "Attendo che Postgres completi il recovery..."
ready=0
for _ in $(seq 1 30); do
    sleep 2
    if docker exec -u postgres "$RESTORE_CONTAINER" pg_isready -U "$PG_USER" -d "$PG_DB" >/dev/null 2>&1; then
        ready=1
        break
    fi
done
if [ "$ready" -ne 1 ]; then
    errore_messaggio="Postgres non è diventato pronto entro il timeout — vedi 'docker logs $RESTORE_CONTAINER'"
    log "ERRORE: $errore_messaggio"
    exit 1
fi
log "Postgres ripristinato è pronto."

all_match=1
for table in "${TABLES_TO_COMPARE[@]}"; do
    restored_count=$(docker exec -u postgres "$RESTORE_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -t -A -c "SELECT count(*) FROM $table;" | tr -d '[:space:]')
    live_count=$(docker exec "$LIVE_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -t -A -c "SELECT count(*) FROM $table;" | tr -d '[:space:]')
    if [ "$restored_count" = "$live_count" ]; then
        log "OK: $table — ripristinato=$restored_count, live=$live_count (combaciano)."
    else
        log "ATTENZIONE: $table — ripristinato=$restored_count, live=$live_count (NON combaciano — normale solo se ci sono state scritture reali dopo l'ultimo backup)."
        all_match=0
    fi
done

success=$all_match
