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
docker-compose.yml   postgres, api, worker, frontend
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

## Integrazioni esterne mantenute

- Wubook (channel manager OTA)
- Alloggiati Web / Questura (schedine)
- Osservatorio Turistico
- PayTourist (tassa di soggiorno)
- Backend licenze/abbonamenti esistente (`GestiSoftWeb/UserService`, esterno — non fa parte di
  questo repo, si continua a integrarlo via HTTP come nel sistema legacy)
