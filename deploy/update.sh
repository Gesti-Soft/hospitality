#!/usr/bin/env bash
# Aggiornamento della VPS di produzione: git pull + rebuild + restart. Le migrazioni EF si
# applicano da sole all'avvio dell'api (vedi Program.cs) — non serve più un passo manuale a parte.
# Uso: ./deploy/update.sh (dalla root del repo, o da qualunque cartella)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "=== git pull ==="
git pull

# Versione installata: all'avvio l'Api notifica l'aggiornamento a tutte le strutture, una volta
# per commit (un pull senza novità o un riavvio non la ripetono).
export GESTISOFT_VERSIONE="$(git rev-parse --short HEAD)"

echo "=== build immagini ==="
docker compose build

echo "=== riavvio servizi ==="
docker compose up -d

echo "=== stato ==="
docker compose ps

#echo "=== log avvio api (Ctrl+C per uscire) ==="
#docker compose logs -f --tail=30 api
