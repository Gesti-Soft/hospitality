#!/usr/bin/env bash
# Aggiornamento della VPS (produzione, o test nella sua cartella): git pull + rebuild + restart. Le migrazioni EF si
# applicano da sole all'avvio dell'api (vedi Program.cs) — non serve più un passo manuale a parte.
# Uso: ./deploy/update.sh (dalla root del repo, o da qualunque cartella)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "=== git pull ==="
git pull

# Ambiente di test (ramo "test", cartella a sé, test.gestihotel.it): stesso script, ma lo stack deve
# partire con l'override di test, letto da COMPOSE_FILE nel .env (vedi docker-compose.test.env.example).
# Senza, partirebbero worker-schedine e worker: invii veri alle PA e prenotazioni OTA sottratte alla
# produzione. Si controlla dopo il pull, che può aver cambiato ramo.
if [[ "$(git rev-parse --abbrev-ref HEAD)" == "test" ]]; then
  if ! grep -Eq '^COMPOSE_FILE=.*docker-compose\.test\.yml' "$REPO_ROOT/.env" 2>/dev/null \
     || ! grep -Eq '^COMPOSE_PROJECT_NAME=.+' "$REPO_ROOT/.env" 2>/dev/null; then
    echo "ERRORE: ramo test senza COMPOSE_FILE/COMPOSE_PROJECT_NAME di test nel .env (vedi docker-compose.test.env.example). Nessun container toccato." >&2
    exit 1
  fi
  echo "=== ambiente di TEST ==="
fi

# Versione installata, "MAGGIORE.MINORE.CORREZIONE" (es. 2.8.3). La maggiore sta nel file VERSIONE del
# repo e si cambia a mano per i salti importanti (2 → 3: minore e correzione ripartono da 0). Le altre
# due le calcola questo script dai titoli dei commit arrivati dall'ultimo deploy: almeno un "feat" →
# minore +1 e correzione a 0; altrimenti (fix, refactor, docs…) → correzione +1; nessun commit nuovo →
# invariata. Il conteggio vive solo su questo server, in deploy/.versione-installata (non versionato):
# test e produzione hanno ciascuno il proprio. La versione si vede in fondo al menu del Super Admin;
# all'avvio l'Api la scrive nel Log, una volta per versione.
STATO_VERSIONE="$REPO_ROOT/deploy/.versione-installata"
MAGGIORE="$(tr -d '[:space:]' < "$REPO_ROOT/VERSIONE")"
COMMIT="$(git rev-parse --short HEAD)"
MINORE=0
CORREZIONE=0
PRIMA=()
if [[ -f "$STATO_VERSIONE" ]]; then
  read -r -a PRIMA < "$STATO_VERSIONE" || true
fi
# Formato di prima dei tre numeri, "MAGGIORE MINORE COMMIT": la correzione era 0.
if [[ ${#PRIMA[@]} -eq 3 ]]; then
  PRIMA=("${PRIMA[0]}" "${PRIMA[1]}" 0 "${PRIMA[2]}")
fi
if [[ ${#PRIMA[@]} -eq 4 && "${PRIMA[0]}" == "$MAGGIORE" ]]; then
  MINORE="${PRIMA[1]}"
  CORREZIONE="${PRIMA[2]}"
  COMMIT_PRIMA="${PRIMA[3]}"
  if [[ "$COMMIT_PRIMA" != "$COMMIT" ]]; then
    # I titoli si leggono prima e si cercano dopo: con pipefail, "git log | grep -q" può fallire
    # per il SIGPIPE di git anche quando grep ha trovato.
    if TITOLI="$(git log --format=%s "$COMMIT_PRIMA..$COMMIT" 2>/dev/null)" && ! grep -qE '^feat(\(|!|:)' <<< "$TITOLI"; then
      CORREZIONE=$((CORREZIONE + 1))
    else
      # Una funzione nuova, oppure il commit di prima non si trova più (storia riscritta): minore +1.
      MINORE=$((MINORE + 1))
      CORREZIONE=0
    fi
  fi
fi
export GESTISOFT_VERSIONE="$MAGGIORE.$MINORE.$CORREZIONE"
echo "=== versione $GESTISOFT_VERSIONE (commit $COMMIT) ==="

echo "=== build immagini ==="
docker compose build

echo "=== riavvio servizi ==="
docker compose up -d

# Solo a deploy riuscito (set -e): un build fallito non consuma un numero di versione.
echo "$MAGGIORE $MINORE $CORREZIONE $COMMIT" > "$STATO_VERSIONE"

echo "=== stato ==="
docker compose ps

#echo "=== log avvio api (Ctrl+C per uscire) ==="
#docker compose logs -f --tail=30 api
