# Deploy in produzione — VPS OVH/Debian

Runbook per il primo deploy reale, passo per passo, con i dati esistenti (non si parte vuoti).
Tutti i comandi vanno eseguiti **sulla VPS** via SSH, salvo dove indicato "in locale".

Target: `hospitality.gestisoft.it` (sottodominio dedicato — **non** un sottopercorso di
`gestisoft.it`, che punta a un'altra VM già in produzione con `GestiSoftWeb`: usare un
sottodominio evita ogni modifica a quel server esistente).

## 0. Prerequisiti

- VPS Debian con Docker + Docker Compose plugin già installati (verificare con `docker compose version`).
- Un record DNS **A** per `hospitality.gestisoft.it` che punta all'IP pubblico della VPS
  (aggiunto dal pannello OVH — non è qualcosa che si fa da qui). Aspettare la propagazione
  (`dig +short hospitality.gestisoft.it` deve restituire l'IP giusto, confrontabile con
  `curl -4 -s https://icanhazip.com` eseguito sulla VPS) prima del passo 7.
- **Importante — VPS condivisa, non vuota**: `vps-5e0dcc6f` ospita già molti altri progetti
  (didattic-sito, royalwatchery, managesuite, ecc.) e usa **Virtualmin**, che gestisce un Apache di
  sistema con un virtual host per dominio — le porte 80/443 sono quindi **sempre già occupate** da
  quell'Apache (non un problema: è così che funziona il resto della VPS, vedi punto 7 più sotto,
  niente Caddy qui). Prima di scegliere le porte host per Postgres/Api/Frontend (punto 2),
  verificare che siano libere — non dare per scontati i default:
  ```bash
  sudo ss -tulpn | grep :<porta>
  # oppure, per porte già pubblicate da Docker altrove:
  docker ps --filter "publish=<porta>"
  ```
  (successo il 2026-09-09: `5432` presa da `managesuite-db`, `8081` da `gestisoft-phpmyadmin` —
  usati `5433`/`8082` al loro posto nel `.env`).

## 1. Codice sulla VPS

```bash
git clone <url-del-repo> gestisoft-gestionale
cd gestisoft-gestionale
```

## 2. `.env` di produzione

```bash
cp docker-compose.env.example .env
nano .env   # o l'editor che preferisci
```

Valori da impostare:

| Variabile | Valore |
|---|---|
| `POSTGRES_PASSWORD` | **Nuova**, generata apposta — mai riusare quella di sviluppo (`openssl rand -base64 32`) |
| `JWT_SECRET` | **Nuovo**, generato apposta (`openssl rand -base64 48`) — invalida tutte le sessioni esistenti al primo avvio, normale |
| `CREDENZIALI_CHIAVE_CIFRATURA` | **Nuova**, generata apposta (`openssl rand -base64 32`) — cifra a riposo le credenziali dei servizi esterni salvate nel database. Obbligatoria: senza, l'Api non parte. Va conservata insieme agli altri segreti del server: perdendola, le credenziali già salvate diventano illeggibili e vanno reinserite a mano |
| `FRONTEND_ORIGIN` | `https://hospitality.gestisoft.it` |
| `SUPERADMIN_EMAIL` / `SUPERADMIN_PASSWORD` | Lasciare pure i valori di esempio — **non verranno usati**: il database ripristinato al passo 5 porta già il vero Super Admin, il seeder si ferma da solo appena trova un Super Admin già esistente (nessun rischio di duplicazione) |
| `GESTISOFT_BASE_URL`, `ALLOGGIATIWEB_ENDPOINT`, `OSSERVATORIO_BASE_URL`, `PAYTOURIST_BASE_URL` | **Stessi valori già in uso in locale** (`.env` locale) — sono gli endpoint reali delle integrazioni esterne già funzionanti oggi per Villa Chifeci Scopello, non vanno cambiati |
| `POSTGRES_HOST_PORT` / `API_HOST_PORT` / `FRONTEND_HOST_PORT` | Restano legati a `127.0.0.1`, non raggiungibili dall'esterno (solo l'Apache di Virtualmin espone qualcosa pubblicamente, vedi punto 7) — **verificare che siano libere prima di avviare**, vedi nota al punto 0 |

## 3. Build (senza avviare nulla)

```bash
docker compose build
```

## 4. Postgres da solo, in attesa dei dati

```bash
docker compose up -d postgres
docker compose ps   # attendere "healthy"
```

## 5. Migrazione dei dati reali

**In locale**, un dump fresco (stesso comando già usato dal backup notturno, o il pulsante
"Scarica backup adesso" nel pannello Super Admin):

```powershell
docker exec -u postgres gestisoft-gestionale-postgres pg_dump -U gestisoft -d gestisoft -Fc -f /tmp/deploy.dump
docker cp gestisoft-gestionale-postgres:/tmp/deploy.dump ./deploy.dump
```

Trasferimento sulla VPS:

```bash
scp ./deploy.dump utente@IP-DELLA-VPS:~/deploy.dump
```

**Sulla VPS**, ripristino nel Postgres appena creato (ancora vuoto — nessun `--clean` necessario):

```bash
docker cp ~/deploy.dump gestisoft-gestionale-postgres:/tmp/deploy.dump
docker exec -u postgres gestisoft-gestionale-postgres pg_restore -U gestisoft -d gestisoft /tmp/deploy.dump
docker exec -u postgres gestisoft-gestionale-postgres rm /tmp/deploy.dump
```

**Verifica prima di proseguire** (non fidarsi alla cieca — stesso principio già applicato in ogni
migrazione fatta finora):

```bash
docker exec gestisoft-gestionale-postgres psql -U gestisoft -d gestisoft -c "SELECT count(*) FROM prenotazioni;"
```

Il numero deve combaciare con quello in locale.

## 6. Avvio completo

```bash
docker compose up -d
docker compose logs -f api   # Ctrl+C per uscire quando si vede "Now listening on..."
```

Nei log dell'avvio dell'`api` deve comparire `No migrations were applied. The database is already
up to date.` (la migrazione automatica introdotta in `Program.cs` — se invece ne applica qualcuna è
perché il codice deployato è più recente del dump: normale, nessun problema).

Verifica interna (ancora senza dominio pubblico):

```bash
curl http://127.0.0.1:5080/health
```

Deve rispondere `{"status":"ok",...}`.

## 7. Reverse proxy pubblico — Apache/Virtualmin, non Caddy

`docker-compose.prod.yml` (che aggiunge un container Caddy) **non si usa su questa VPS**: Virtualmin
gestisce già un Apache di sistema con un virtual host per dominio (incluso `hospitality.gestisoft.it`,
creato automaticamente con certificato SSL già valido quando è stato aggiunto l'account) — Caddy
competerebbe per le stesse porte 80/443 e fallirebbe ad avviarsi (successo il 2026-09-09). Si
riusa invece l'Apache esistente, seguendo lo stesso schema già adottato per gli altri siti con
frontend/backend separati su questa VPS (es. `managesuite`, vedi il suo
`/etc/apache2/sites-enabled/managesuite.*.conf` come riferimento):

```bash
sudo nano /etc/apache2/sites-enabled/hospitality.gestisoft.it.conf
```

Nel blocco `<VirtualHost *:443>` (quello con `SSLEngine on`), subito dopo la riga
`ProxyPass /.well-known !`, aggiungere:

```apache
    ProxyPreserveHost On
    ProxyRequests Off
    ProxyPass / http://127.0.0.1:8082/
    ProxyPassReverse / http://127.0.0.1:8082/
```

(`8082` è il valore scelto per `FRONTEND_HOST_PORT` — usare quello effettivamente impostato nel
`.env` di questa VPS se diverso). Non serve toccare il blocco `*:80`: il sito resta comunque
raggiungibile in HTTPS. Poi:

```bash
sudo apachectl configtest   # deve dire "Syntax OK" — se dà errore, NON procedere
sudo systemctl reload apache2
```

`reload` (non `restart`) ricarica la configurazione di *tutti* i siti su questa Apache senza
downtime — sicuro anche su una VPS condivisa, a patto che `configtest` sia passato prima. Verifica:

```bash
curl -I https://hospitality.gestisoft.it
```

Deve rispondere `200` con `server: nginx` (il container `frontend`, raggiunto attraverso il proxy
Apache) — non più `server: Apache` (la pagina statica di default di Virtualmin).

## 8. Verifica finale

- Login reale dal browser su `https://hospitality.gestisoft.it` con il Super Admin vero (non
  uno di test) — deve funzionare con la password già in uso oggi.
- Un giro nell'app: Clienti, una Struttura, il Cruscotto — i dati devono essere gli stessi di oggi
  in locale.
- `docker compose ps` — tutti i container `Up`, `postgres` `healthy`.

## 9. Backup automatici sulla VPS

Stessa logica già in produzione qui in locale (pgBackRest + pg_dump + test di restore mensile),
tradotta in bash — vedi `docker/postgres/scripts/*.sh` (equivalenti esatti dei 3 script
PowerShell, stessa tabella `log_eventi`/pagina Backup del pannello Super Admin).

**Setup iniziale pgBackRest** (una tantum, stessa sequenza già fatta in locale — vedi
`docs/backup-restore.md` per il dettaglio di ogni passo):

```bash
docker exec -u postgres gestisoft-gestionale-postgres pgbackrest --stanza=gestisoft stanza-create
docker exec -u postgres gestisoft-gestionale-postgres pgbackrest --stanza=gestisoft check
docker exec -u postgres gestisoft-gestionale-postgres pgbackrest --stanza=gestisoft --type=full backup
```

Rendere eseguibili gli script e testare subito il restore (non aspettare il primo giro mensile):

```bash
chmod +x docker/postgres/scripts/*.sh
./docker/postgres/scripts/test-restore.sh
```

Deve concludersi con `Test di restore RIUSCITO`. Solo dopo, registrare i cron job (`crontab -e`,
percorsi assoluti — sostituire `/percorso/assoluto` con il path reale del repo clonato):

```cron
0 3 * * * /percorso/assoluto/docker/postgres/scripts/backup-nightly.sh >> /percorso/assoluto/docker/postgres/logs/cron-backup.log 2>&1
0 * * * * /percorso/assoluto/docker/postgres/scripts/check-archiver.sh >> /percorso/assoluto/docker/postgres/logs/cron-check.log 2>&1
0 4 */28 * * /percorso/assoluto/docker/postgres/scripts/test-restore.sh >> /percorso/assoluto/docker/postgres/logs/cron-restore.log 2>&1
```

**Nota sul nome del volume repo**: `test-restore.sh` assume il nome
`gestisoftgestionale_gestisoft_pgbackrest_repo` (derivato dal nome della cartella del progetto). Se
il repo è clonato con un nome diverso da `gestisoft-gestionale`, verificare il nome reale con
`docker volume ls | grep pgbackrest_repo` e correggere la variabile `REPO_VOLUME` in cima allo
script prima di lanciarlo.

## Rollback / in caso di problema

Nessuna azione qui è distruttiva verso l'ambiente locale: il dump è una copia, l'originale
continua a girare in locale indipendentemente da come va il deploy. Se qualcosa non funziona sulla
VPS:

```bash
docker compose down
```

ferma tutto senza cancellare i volumi (dati Postgres restano). Si può correggere e ripartire dal
punto 6 senza dover rifare la migrazione dati (già nel volume). Il virtual host Apache/Virtualmin
(punto 7) non viene toccato da questo comando — resterà a servire l'ultima risposta ottenuta
dall'app finché i container non ripartono.

## Aggiornamenti futuri

Da questo deploy in poi, un aggiornamento è solo:

```bash
bash ./deploy/update.sh
```

(`git pull` + rebuild + restart dei container di questo repo — niente Caddy, vedi punto 7 — con
log dell'`api` mostrati a schermo per controllare che la migrazione — automatica da questo deploy
in poi, vedi `Program.cs` — sia andata a buon fine). Non serve più alcun passo manuale
`dotnet ef database update` a parte. Il virtual host Apache/Virtualmin (punto 7) resta invariato
tra un aggiornamento e l'altro: va toccato di nuovo solo se cambia la porta `FRONTEND_HOST_PORT`.
