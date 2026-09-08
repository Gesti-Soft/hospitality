# Deploy in produzione — VPS OVH/Debian

Runbook per il primo deploy reale, passo per passo, con i dati esistenti (non si parte vuoti).
Tutti i comandi vanno eseguiti **sulla VPS** via SSH, salvo dove indicato "in locale".

Target: `gsthospitality.gestisoft.it` (sottodominio dedicato — **non** un sottopercorso di
`gestisoft.it`, che punta a un'altra VM già in produzione con `GestiSoftWeb`: usare un
sottodominio evita ogni modifica a quel server esistente).

## 0. Prerequisiti

- VPS Debian con Docker + Docker Compose plugin già installati (verificare con `docker compose version`).
- Un record DNS **A** per `gsthospitality.gestisoft.it` che punta all'IP pubblico della VPS
  (aggiunto dal pannello OVH — non è qualcosa che si fa da qui). Aspettare la propagazione
  (`dig gsthospitality.gestisoft.it` deve restituire l'IP giusto) prima del passo 8.
- Porte 80 e 443 raggiungibili dall'esterno sulla VPS (nessun altro servizio le occupa già —
  verificare con `sudo ss -tlnp | grep -E ':80|:443'`, deve risultare vuoto prima di avviare Caddy).

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
| `FRONTEND_ORIGIN` | `https://gsthospitality.gestisoft.it` |
| `SUPERADMIN_EMAIL` / `SUPERADMIN_PASSWORD` | Lasciare pure i valori di esempio — **non verranno usati**: il database ripristinato al passo 5 porta già il vero Super Admin, il seeder si ferma da solo appena trova un Super Admin già esistente (nessun rischio di duplicazione) |
| `GESTISOFT_BASE_URL`, `ALLOGGIATIWEB_ENDPOINT`, `OSSERVATORIO_BASE_URL`, `PAYTOURIST_BASE_URL` | **Stessi valori già in uso in locale** (`.env` locale) — sono gli endpoint reali delle integrazioni esterne già funzionanti oggi per Villa Chifeci Scopello, non vanno cambiati |
| `POSTGRES_HOST_PORT` / `API_HOST_PORT` / `FRONTEND_HOST_PORT` | Lasciare i default — restano legati a `127.0.0.1`, non raggiungibili dall'esterno; solo Caddy espone qualcosa pubblicamente |

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

Verifica interna (ancora senza Caddy/dominio):

```bash
curl http://127.0.0.1:5080/health
```

Deve rispondere `{"status":"ok",...}`.

## 7. Caddy (reverse proxy + HTTPS automatico)

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f caddy
```

La prima volta Caddy ottiene un certificato Let's Encrypt in automatico (serve che il DNS sia già
propagato, vedi punto 0) — nei log si vede `certificate obtained successfully`. Poi:

```bash
curl -I https://gsthospitality.gestisoft.it
```

Deve rispondere `200`, con un certificato valido (nessun avviso in un browser reale).

## 8. Verifica finale

- Login reale dal browser su `https://gsthospitality.gestisoft.it` con il Super Admin vero (non
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
docker compose -f docker-compose.yml -f docker-compose.prod.yml down
```

ferma tutto senza cancellare i volumi (dati Postgres e certificati Caddy restano). Si può
correggere e ripartire dal punto 6 senza dover rifare la migrazione dati (già nel volume).

## Aggiornamenti futuri

Da questo deploy in poi, un aggiornamento è solo:

```bash
./deploy/update.sh
```

(`git pull` + rebuild + restart con Caddy incluso, log dell'`api` mostrati a schermo per
controllare che la migrazione — automatica da questo deploy in poi, vedi `Program.cs` — sia andata
a buon fine). Non serve più alcun passo manuale `dotnet ef database update` a parte.
