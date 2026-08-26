# Session report — Migrazione GestiSoft a Web

Ultimo aggiornamento: 2026-08-26 (in corso, sessione di scaffolding iniziale — Fase 0)

## Dove si trova tutto

- **Piano approvato** (fonte di verità per architettura/decisioni/fasi): `C:\Users\Gaetano\.claude\plans\precious-dreaming-summit.md`
- **Codice legacy da migrare** (solo lettura, NON toccare): `c:\Code\GestiSoft\Spostare a web\` (Controller, GestiSoft [WPF], OrderManagement, OtaService, SyncStatePolice)
- **Backend licenze/abbonamenti esistente, già in produzione** (esterno, da NON riscrivere, solo integrare via HTTP): `c:\Code\GestiSoft\GestiSoftWeb\UserService` — ASP.NET Core 9 + MySQL, path base `/gst-admin`, endpoint chiave: `POST /users/check-subscription`, `POST /users/set-running` (usati oggi da Controller/OtaService via env var `gestisoft`).
- **Nuovo progetto (questo lavoro)**: `c:\Code\GestiSoft\GestiSoftGestionale\`
  - `backend/` — soluzione .NET 10 (`GestiSoft.slnx`)
  - `frontend/` — React (da creare, non ancora iniziato)
  - `docker/`, `docs/` — cartelle create, vuote

## Decisioni chiave già prese (vedi piano per dettagli completi)

- Riscrittura completa da zero, stessa logica di business, RabbitMQ/GestiSoftQueues eliminato → chiamate dirette.
- DB: **PostgreSQL**. Hosting: **on-premise/VPS via Docker Compose** (UN SOLO deployment, non uno per cliente — vedi sotto). Backend: **C#/.NET 10**. Frontend: **React**.
- Tenancy: **AGGIORNATO (decisione finale)** — un unico gestionale multi-tenant, un solo `docker-compose` per tutti i clienti. Gerarchia a tre livelli:
  - **Super Admin** (staff GestiSoft) → vede/amministra tutti i Clienti.
  - **Cliente** (azienda/gestore, vero tenant) → vede solo le proprie Strutture.
  - **Struttura** (singola proprietà) → appartiene a un Cliente (`ClienteId`); ruoli utente definiti per singola struttura (tabella ponte `UtenteStruttura`).
  - Già implementato in `GestiSoft.Domain`: entità `Cliente` (`Entities/Cliente.cs`) 1→N `Struttura` (`Entities/Struttura.cs`, ha `ClienteId`). Da fare in Fase 2 (Auth): entità `Utente`, `UtenteStruttura` (ruolo per coppia utente/struttura), claim JWT (`IsSuperAdmin`, `ClienteId`, elenco Struttura+Ruolo accessibili).
- Frontend: design **non standard/generico "AI-generated"** — richiesta esplicita dell'utente di un'identità visiva propria, non il look di default di librerie usate "out of the box". Passata di design dedicata prevista prima di implementare le schermate (Fase 9 del piano).
- **Solo librerie/pacchetti sicuri e verificati** (ufficiali, ampiamente usati) — richiesta esplicita dell'utente, controllare ogni pacchetto NuGet/npm prima di installarlo.
- Backend "gestisoft" (licenze/abbonamenti in `GestiSoftWeb/UserService`) resta **esterno**: il nuovo gestionale continua a integrarcisi via HTTP (stesso pattern di oggi), non va riscritto/duplicato.

## Stato avanzamento — Fase 0 (scaffolding backend)

Fatto finora:
1. Creata cartella progetto `c:\Code\GestiSoft\GestiSoftGestionale\` con sottocartelle `backend/src`, `backend/tests`, `frontend`, `docker`, `docs`.
2. Solution .NET creata: `backend/GestiSoft.slnx`, target `net10.0`.
3. Progetti creati e aggiunti alla solution, con reference tra layer già cablate:
   - `GestiSoft.Domain` (classlib) — entità dominio
   - `GestiSoft.Contracts` (classlib) — DTO condivisi con frontend
   - `GestiSoft.Application` (classlib, → Domain, Contracts) — logica di business
   - `GestiSoft.Infrastructure` (classlib, → Domain, Application) — EF Core/Npgsql, client HTTP esterni
   - `GestiSoft.Api` (webapi con controller, → Application, Infrastructure, Contracts)
   - `GestiSoft.Worker` (worker service, → Application, Infrastructure)
   - `GestiSoft.Application.Tests`, `GestiSoft.Api.Tests` (xunit)
4. Pacchetti NuGet aggiunti (tutti ufficiali/ampiamente noti, verificati):
   - Infrastructure: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`
   - Api: `Serilog.AspNetCore` (porta con sé Console sink transitivamente)
   - Worker: `Serilog.Extensions.Hosting`, `Serilog.Sinks.Console`
5. File di dominio/infrastruttura scritti e **backend che compila senza errori/warning** (`dotnet build` in `backend/` → 0 errori, 0 warning):
   - `Domain/Common/TenantEntity.cs` — base class con `Id`, `StrutturaId`, `CreatedAtUtc`, `UpdatedAtUtc`
   - `Domain/Entities/Cliente.cs` — radice tenant reale (azienda/gestore), 1→N Strutture
   - `Domain/Entities/Struttura.cs` — property/hotel, `ClienteId` FK verso Cliente
   - `Contracts/Health/HealthResponse.cs`
   - `Application/DependencyInjection.cs` — placeholder `AddApplication()`
   - `Infrastructure/DependencyInjection.cs` — `AddInfrastructure(IConfiguration)`, registra `GestiSoftDbContext` con Npgsql leggendo `ConnectionStrings:Default`
   - `Infrastructure/Persistence/GestiSoftDbContext.cs` (DbSet Clienti, Strutture)
   - `Infrastructure/Persistence/GestiSoftDbContextFactory.cs` — `IDesignTimeDbContextFactory` per `dotnet ef migrations` da CLI (connection string da env var `GESTISOFT_CONNECTION_STRING`, fallback locale)
   - `Infrastructure/Persistence/Configurations/ClienteConfiguration.cs`, `StrutturaConfiguration.cs`
   - `Api/Program.cs` — Serilog console, `AddInfrastructure`, `AddApplication`, CORS verso `http://localhost:5173`, `GET /health`. Rimossi i file placeholder del template (`WeatherForecast.cs`/`WeatherForecastController.cs`).
   - `Worker/Program.cs` — Serilog (logger globale + `AddSerilog()`), `AddInfrastructure`, `AddApplication`, `AddHostedService<Worker>()` (il body di `Worker.cs` è ancora il placeholder del template — solo un heartbeat di log, va sostituito nelle Fasi 5-8 con i job schedulati reali)
   - `appsettings.json`/`appsettings.Development.json` di Api e Worker con `ConnectionStrings:Default` (dev: password placeholder locale `gestisoft_dev_only`, NON un segreto reale — in produzione va iniettata via env var `ConnectionStrings__Default`)
   - Pacchetti NuGet aggiunti (tutti ufficiali, verificati): `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.Relational` (pinnato a 10.0.11 per risolvere un conflitto di versione con Npgsql 10.0.3 che referenziava EF Core 10.0.4), `Serilog.AspNetCore` (Api), `Serilog.Extensions.Hosting`+`Serilog.Sinks.Console` (Worker), `Microsoft.Extensions.DependencyInjection.Abstractions` (Application).
6. **Frontend** scaffoldato in `frontend/` con `npm create vite@latest -- --template react-ts` (tool ufficiale Vite) → React 19, Vite 8, TypeScript, oxlint. `npm install` eseguito, **0 vulnerabilità**. Aggiunte (verificate su npm prima dell'installazione, pacchetti ufficiali/standard): `react-router-dom` (remix-run), `@tanstack/react-query`. Non ancora usate nel codice (solo installate) — routing/layout/chiamata health-check verso l'Api ancora da scrivere.

## ⚠️ Decisione importante cambiata a metà scaffolding — leggere prima di continuare

L'utente ha corretto la tenancy DOPO che Fase 0 era già iniziata: **non più "un deployment per cliente"**, ma **un unico gestionale multi-tenant** con gerarchia Super Admin → Cliente → Struttura → Ruoli-per-struttura (vedi sopra "Decisioni chiave" e il piano approvato, già aggiornati). Le entità `Cliente`/`Struttura` sono già state adattate a questo modello. Non esiste ancora `Utente`/`UtenteStruttura` — vanno creati in Fase 2, non prima.

## Fatto — resto di Fase 0 (scaffolding completato)

7. **Frontend**: routing con `react-router-dom` (`src/routes/AppRouter.tsx`), `App.tsx` con `QueryClientProvider`, `src/api/client.ts` (fetch wrapper, base URL da `VITE_API_URL`, default `http://localhost:5080`), `src/api/health.ts` (hook TanStack Query su `GET /health`), `src/pages/DashboardPage.tsx` mostra lo stato di connessione all'Api. `npm run build` (tsc + vite build) **passa senza errori**. Ripulita l'estetica di default del template Vite (accento viola `#aa3bff` ecc. in `index.css`, `App.css`, asset demo) — sostituita con un reset minimale neutro, coerente con la richiesta esplicita di NON avere il look "standard AI-generated"; il design vero arriva in Fase 9.
8. **Docker**: `Dockerfile` per Api (`backend/src/GestiSoft.Api/Dockerfile`, multi-stage SDK→aspnet runtime, utente non-root) e Worker (`backend/src/GestiSoft.Worker/Dockerfile`, multi-stage SDK→runtime, utente non-root); `frontend/Dockerfile` (multi-stage node→nginx) + `frontend/nginx.conf` (SPA fallback + proxy `/api/` → container `api:8080`). `docker-compose.yml` alla radice: servizi `postgres` (17-alpine), `api`, `worker`, `frontend`, un solo stack per l'intero gestionale multi-tenant (non uno a cliente). `docker-compose.env.example` con variabili (nessun segreto reale in chiaro).
9. `.gitignore` e `README.md` di progetto creati. **Non ho ancora fatto `git init`** nella cartella — da fare quando l'utente conferma (i repo legacy hanno ciascuno il proprio `.git`, va deciso se seguire la stessa convenzione).
10. `docker compose config` (validazione sintassi/interpolazione) → **OK, nessun errore**. `docker compose up -d --build` → **NON verificato**: Docker Desktop non è in esecuzione su questa macchina (`failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine`). Va rilanciato Docker Desktop e ripetuto `docker compose up -d --build` dentro `c:\Code\GestiSoft\GestiSoftGestionale\` per la verifica end-to-end (build immagini + healthcheck Postgres + Api risponde su `/health` + frontend raggiungibile su `http://localhost:8081`).

## Fatto — Fase 0 COMPLETA e verificata end-to-end (Docker Desktop era spento, poi riavviato)

11. **Gestione del carico multi-tenant** (a seguito di una domanda esplicita dell'utente: "come gestire tante chiamate essendo un gestionale unico per più clienti?"): aggiunto al piano e implementato:
    - **Rate limiting** nativo ASP.NET Core (`Microsoft.AspNetCore.RateLimiting`, incluso nel framework, zero pacchetti esterni) in `GestiSoft.Api/Program.cs` — partizionato per `cliente_id` (claim JWT, quando esisterà) con fallback per IP finché non c'è auth. 200 richieste/10s per partizione, HTTP 429 oltre soglia. Protegge da un cliente che satura il sistema a danno degli altri.
    - **Quartz.NET** (`Quartz.Extensions.Hosting`, pacchetto ufficiale, Apache 2.0) nel Worker, al posto del vecchio `BackgroundService` placeholder (rimosso `Worker.cs`). Sostituisce le code RabbitMQ per la schedulazione delle integrazioni esterne: job store attualmente in RAM (default), da passare a persistente su Postgres quando arrivano i job reali (Fase 5-8), con limiti di concorrenza per tipo di integrazione (`[DisallowConcurrentExecution]`) così non si martella Wubook/Alloggiati Web/Osservatorio/PayTourist in parallelo per tutti i clienti insieme. Job dimostrativo: `Jobs/HeartbeatJob.cs` (heartbeat ogni minuto, verificato nei log Docker).
12. **Verifica end-to-end completa con Docker Desktop attivo**:
    - Aggiunto `.dockerignore` (root e `frontend/`) — necessario perché le cartelle `obj/` generate su Windows contenevano path NuGet assoluti Windows-only che rompevano la build nel container Linux (`Unable to find fallback package folder 'C:\Program Files (x86)\...'`). Senza escludere `obj/`/`bin/` dal contesto Docker, la build fallisce sempre su Windows.
    - `docker compose up -d --build` → **tutti i container su, Postgres healthy**, Api risponde su `http://localhost:5080/health` e anche via proxy Nginx su `http://localhost:8081/api/health`, frontend servito su `http://localhost:8081/` (200 OK), Worker/Quartz logga l'heartbeat ogni minuto.
13. **Prima migration EF Core**: aggiunto `Microsoft.EntityFrameworkCore.Design` anche al progetto Api (richiesto dal tool `dotnet ef` sul progetto di startup, non basta averlo solo su Infrastructure). Tool globale `dotnet-ef` aggiornato da 10.0.9 a 10.0.11 (per allinearsi al runtime). Migration `InitialCreate` generata in `GestiSoft.Infrastructure/Persistence/Migrations/` e **applicata con successo** al Postgres del container (`dotnet ef database update` con `GESTISOFT_CONNECTION_STRING` puntato a `localhost:5432`). Verificato con `psql \dt`: tabelle `clienti`, `strutture`, `__EFMigrationsHistory` create correttamente.

**Stato Docker Compose lasciato**: lo stack è **ancora in esecuzione** su questa macchina (container `gestisoft-gestionale-{postgres,api,worker,frontend}`). Per fermarlo: `docker compose down` (aggiungere `-v` solo se si vuole anche cancellare i dati Postgres) dentro `c:\Code\GestiSoft\GestiSoftGestionale\`.

## Prossimi passi immediati (da riprendere qui)

1. Decidere con l'utente se fare `git init` nella nuova cartella ora o più avanti (non ancora fatto).
2. **Fase 1 — Dominio & Database**: completare lo schema Postgres (camere/tipologie, prenotazioni, ospiti/schede alloggiati, prezzi, finanze, fatturazione — vedi mappatura entità nel piano), seed Comuni/Stati/Documenti/TipoAlloggiato come risorse embedded + migration (oggi nel legacy lette da path assoluto Windows `C:\GestiSoft\Hospitality\OrderManagement\`, vedi file `comuni.txt`/`stati.txt`/`documenti.txt`/`tipo_alloggiato.txt` in `OrderManagement/OrderManagement/` nel progetto legacy).
3. **Fase 2 — Auth & Core API**: entità `Utente`, `UtenteStruttura` (ruolo per coppia utente/struttura), claim JWT (`IsSuperAdmin`, `cliente_id`, elenco Struttura+Ruolo) — il rate limiting già scaffoldato in Fase 0 legge già il claim `cliente_id`, quindi l'integrazione sarà diretta una volta pronta l'auth.

## Note importanti da non perdere

- Path assoluto hardcoded da NON riportare: il vecchio `OrderManagement` legge `comuni.txt`/`stati.txt`/`documenti.txt`/`tipo_alloggiato.txt` da `C:\GestiSoft\Hospitality\OrderManagement\` — nel nuovo sistema questi vanno incorporati come risorse embedded + seed migration EF Core.
- Il client Wubook (XML-RPC) e i client Alloggiati Web/Osservatorio Turistico/PayTourist nel codice legacy sono già scritti con `HttpClient` puro, **senza dipendenza da RabbitMQ** — sono la parte più facilmente portabile as-is (vedi `OtaService.Data/Repositories/OtaService/OtaServiceApiRepository.cs` e `SyncStatePolice.Data/Repositories/Statepolice/StatePoliceApiRepository.cs` nel progetto legacy).
- `OtaService.Web` (dentro il progetto legacy `OtaService`) ha già controller REST (`RoomsController`, `PricingPlansController`, `RestrictionsController`, `ConfigController`) che wrappano la logica Wubook — ottimo punto di partenza da portare quasi identico nella Fase 5.
