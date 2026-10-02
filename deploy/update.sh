#!/usr/bin/env bash
# Aggiornamento della VPS di produzione: git pull + rebuild + restart. Le migrazioni EF si
# applicano da sole all'avvio dell'api (vedi Program.cs) — non serve più un passo manuale a parte.
# Uso: ./deploy/update.sh (dalla root del repo, o da qualunque cartella)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "=== git pull ==="
git pull

# Versione installata, "MAGGIORE.MINORE": la maggiore sta nel file VERSIONE del repo e si cambia a
# mano per gli aggiornamenti strutturali importanti (2 → 3); la minore la conta questo script, +1 a
# ogni deploy che porta codice nuovo, e riparte da 0 quando cambia la maggiore. Il conteggio vive
# solo su questo server, in deploy/.versione-installata (non versionato). All'avvio l'Api notifica
# l'aggiornamento a tutte le strutture, una volta per versione: un pull senza novità o un riavvio
# non la ripetono.
STATO_VERSIONE="$REPO_ROOT/deploy/.versione-installata"
MAGGIORE="$(tr -d '[:space:]' < "$REPO_ROOT/VERSIONE")"
COMMIT="$(git rev-parse --short HEAD)"
MINORE=0
if [[ -f "$STATO_VERSIONE" ]]; then
  read -r MAGGIORE_PRIMA MINORE_PRIMA COMMIT_PRIMA < "$STATO_VERSIONE"
  if [[ "$MAGGIORE_PRIMA" == "$MAGGIORE" ]]; then
    if [[ "$COMMIT_PRIMA" == "$COMMIT" ]]; then
      MINORE="$MINORE_PRIMA"
    else
      MINORE=$((MINORE_PRIMA + 1))
    fi
  fi
fi
export GESTISOFT_VERSIONE="$MAGGIORE.$MINORE"
echo "=== versione $GESTISOFT_VERSIONE (commit $COMMIT) ==="

echo "=== build immagini ==="
docker compose build

echo "=== riavvio servizi ==="
docker compose up -d

# Solo a deploy riuscito (set -e): un build fallito non consuma un numero di versione.
echo "$MAGGIORE $MINORE $COMMIT" > "$STATO_VERSIONE"

echo "=== stato ==="
docker compose ps

#echo "=== log avvio api (Ctrl+C per uscire) ==="
#docker compose logs -f --tail=30 api
