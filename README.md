# GestiSoft Gestionale (web)

Riscrittura web del gestionale GestiSoft (PMS per strutture ricettive): un unico gestionale
multi-tenant (Super Admin → Cliente → Struttura, ruoli per struttura), containerizzato con
Docker, backend C#/.NET, frontend React, database PostgreSQL. Sostituisce il sistema desktop
WPF + RabbitMQ esistente, mantenendo identica la logica di business e tutte le integrazioni
esterne (Wubook, Alloggiati Web/schedine, Osservatorio Turistico, PayTourist).

Piano completo e decisioni architetturali: vedi `sessionreport.md` in questa cartella (stato
avanzamento e prossimi passi) e il piano approvato in
`C:\Users\Gaetano\.claude\plans\precious-dreaming-summit.md`.

## Struttura

```
backend/    soluzione .NET 10 (Domain, Application, Infrastructure, Contracts, Api, Worker)
frontend/   React + TypeScript + Vite
docker/     immagine Postgres custom (pgBackRest incluso) + script di backup
deploy/     Caddyfile e script per il deploy in produzione
docs/       runbook (backup/restore, deploy)
docker-compose.yml        postgres, api, worker, worker-schedine, frontend (sviluppo/base)
docker-compose.prod.yml   override che aggiunge Caddy (reverse proxy + TLS) per la produzione
```

## Sviluppo locale (senza Docker)

Backend:
```
cd backend
dotnet build
dotnet run --project src/GestiSoft.Api
dotnet run --project src/GestiSoft.Worker
```
Richiede un'istanza PostgreSQL locale raggiungibile con la connection string in
`backend/src/GestiSoft.Api/appsettings.Development.json` (valori di sviluppo, non segreti reali).

Frontend:
```
cd frontend
npm install
npm run dev
```
Apre su `http://localhost:5173`, punta all'Api su `http://localhost:5080` (vedi `.env.example`).

## Avvio con Docker Compose

```
cp docker-compose.env.example .env
# modifica .env con una password Postgres reale
docker compose up -d --build
```

- Frontend: `http://localhost:8081`
- Api: `http://localhost:5080`
- Postgres: `localhost:5432` (solo da localhost)

## Backup del database

Backup fisico + WAL archiving continuo (pgBackRest, retention 30 giorni) e dump logico
indipendente (`pg_dump`, retention 7 giorni), entrambi automatici (Windows Task Scheduler in
locale, cron sulla VPS di produzione — script equivalenti in `docker/postgres/scripts/`, `.ps1` e
`.sh`), con test di restore mensile automatico. Storico e orari visibili anche dal pannello Super
Admin (pagina "Backup"). Vedi `docs/backup-restore.md` per il runbook completo (controlli di
salute, restore di emergenza, come aggiungere una copia off-site).

## Deploy in produzione

VPS Debian, dietro Caddy (reverse proxy + HTTPS automatico), con le migrazioni EF applicate da
sole all'avvio dell'Api. Vedi `docs/deploy.md` per il runbook completo (primo deploy con
migrazione dei dati esistenti, verifica, rollback). Aggiornamenti successivi: `./deploy/update.sh`
(`git pull` + rebuild + restart).

## Integrazioni esterne mantenute

- Wubook (channel manager OTA)
- Alloggiati Web / Questura (schedine)
- Osservatorio Turistico
- PayTourist (tassa di soggiorno)
- Backend licenze/abbonamenti esistente (`GestiSoftWeb/UserService`, esterno — non fa parte di
  questo repo, si continua a integrarlo via HTTP come nel sistema legacy)
