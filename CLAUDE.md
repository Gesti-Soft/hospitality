# GestiSoft Gestionale — Hotel Management System

Auto-loaded by Claude Code at the start of every session.

> **Reply to the user in Italian.** This file is in English for token economy;
> the codebase and the conversation are Italian.

## Project Overview

Multi-tenant web PMS for accommodation businesses: .NET backend + React frontend,
single repo. Hierarchy: **Super Admin → Cliente → Struttura**, with roles and
permissions scoped per Struttura (`UtenteStruttura`).

Progress, decisions and session history: `sessionreport.md`.
Runbooks: `docs/deploy.md`, `docs/backup-restore.md`. See also `README.md`.

### Session report — hard limit

`sessionreport.md` is auto-loaded every session, so its size is a permanent
context cost. **Hard cap: 300 lines.**

Check the line count before appending. If the file is at or over the cap:

- Condense the oldest entries first — keep decisions and their rationale,
  drop step-by-step narration, resolved bugs, and anything superseded by
  later work
- Merge entries about the same feature into a single one
- Never delete an entry that records a decision still in force: rewrite it shorter
- If condensing isn't enough, move the older half to `docs/session-archive.md`
  (not auto-loaded) and leave a one-line pointer

New entries are written short from the start: what changed, why, what's still
open. Not a transcript of the session.

## Auto-Loaded Context Files

Loaded automatically from this folder (project root):

@./sessionreport.md

### Stack

- **Backend**: C# / .NET 10, ASP.NET Core Web API (controllers), EF Core 10 +
  Npgsql, Serilog, Quartz (scheduled jobs), QuestPDF (invoices), JWT + Otp.NET (2FA)
- **Frontend**: React 19 + TypeScript + Vite, MUI 9 (+ MUI X Charts),
  TanStack Query, React Router 7, oxlint
- **Database**: PostgreSQL (EF Core migrations, applied automatically on Api startup)
- **Infrastructure**: Docker Compose; in production a shared Debian VPS hosting other
  sites, behind **Virtualmin's Apache** (per-domain virtual host, SSL handled there).
  Backups: pgBackRest (physical + WAL) and `pg_dump`

### Repo layout

```
backend/src/GestiSoft.Domain          entities and enums
backend/src/GestiSoft.Application     use cases, service interfaces, exceptions
backend/src/GestiSoft.Infrastructure  EF Core, repositories, seeding, external integrations
backend/src/GestiSoft.Contracts       DTOs on the wire to the frontend
backend/src/GestiSoft.Api             controllers, auth, rate limiting, error handling
backend/src/GestiSoft.Worker          Quartz jobs: OTA polling, notifications, log cleanup
backend/src/GestiSoft.WorkerSchedine  Quartz jobs: daily submissions to public authorities
backend/tests/                        xUnit (Api.Tests, Application.Tests)
frontend/src                          pages, components, api, auth, permissions, routes
docker/, deploy/, docs/               Postgres image, update.sh, deploy/backup runbooks
```

Project dependencies: `Api → Application + Infrastructure + Contracts`,
`Infrastructure → Application + Domain`, `Application → Domain + Contracts`.
Domain depends on nothing.

### Services (Docker Compose)

| Service | Container | Host port (localhost only) |
|---|---|---|
| `postgres` | gestisoft-gestionale-postgres | 5432 |
| `api` | gestisoft-gestionale-api | 5080 → 8080 |
| `worker` | gestisoft-gestionale-worker | — |
| `worker-schedine` | gestisoft-gestionale-worker-schedine | — |
| `frontend` | gestisoft-gestionale-frontend | 8081 → 80 (nginx) |

No container is publicly exposed: in production traffic arrives through
Virtualmin's Apache, reverse-proxying to these ports on `127.0.0.1`
(virtual host in `/etc/apache2/sites-enabled/`, see `docs/deploy.md` §7).
The VPS is shared with other projects: **check host ports are free** before starting.

### Database

PostgreSQL, one database for all tenants. Two entity bases:

- `TenantEntity` (`Id`, `StrutturaId`, `CreatedAtUtc`, `UpdatedAtUtc`) — anything
  belonging to a Struttura. **Every query must filter by `StrutturaId`**
- `Entity` (`Id` only) — shared global reference data: Comuni, Stati, Documenti,
  TipoAlloggiato

Configurations in `Infrastructure/Persistence/Configurations/`, migrations in
`Infrastructure/Persistence/Migrations/`. On startup the Api runs `MigrateAsync()`
and the seeders (reference data + identity).

### External integrations

OTA channel manager (Wubook), Alloggiati Web / Questura (schedine), Osservatorio
Turistico, PayTourist (tourist tax), external licensing/subscription service
(`GestiSoftWeb/UserService`, over HTTP).

⚠️ **In the UI always write "OTA", never the vendor name** — Wubook stays only in
internal identifiers (classes, tables, endpoints).

⚠️ **Submissions to public authorities (Questura, Osservatorio, PayTourist) are
irreversible, and even in development the configured credentials are the production
ones**: never submit anything without an explicit request.

---

# 🔴 CRITICAL RULES

These rules take precedence over anything else in this file.

## Rule 1: Never commit or push without consent

**❌ FORBIDDEN:**
- Automatic commits after code changes
- Automatic push to remotes
- Committing without explicit user permission

**✅ REQUIRED:**
- Wait for an EXPLICIT, UNAMBIGUOUS request ("commit", "push",
  "puoi committare", "puoi pushare")
- When in doubt, ASK: "Vuoi che committa e pusha le modifiche?"

Same rule for destructive database operations: migrations, drop, truncate, reset.
Propose them, do not run them.

## Rule 2: A question is not an instruction

If the message is a question, the answer is **text only**. No file changes.

Questions look like: "come funziona X", "perché fa Y", "cosa succede se",
"dove sta", "puoi spiegarmi", "va bene se facciamo così", "secondo te".

- Reading files to answer: **yes**
- Editing files: **no**, until an explicit instruction arrives
- If the answer implies a change: **propose it**, don't apply it
- If it's unclear whether it's a question or a request: **ask**

## Rule 3: Change only what was asked

The scope of a change is what the user specified, nothing more.

**❌ FORBIDDEN without an explicit request:**
- Refactoring, renaming variables/functions, reorganising files
- Reformatting lines not touched by the change
- Adding, updating or removing dependencies
- Deleting commented-out code, logs, someone else's TODOs
- "Improvements" or side fixes noticed along the way
- Creating files that weren't requested

**✅ If a problem outside scope is noticed:** flag it in one line at the end of the
reply. The user decides whether to act.

## Rule 4: Verify after every change

Before declaring a change complete:

1. **Match**: it does exactly what was asked, no more, no less
2. **Impact**: find every call site of what was touched (function, component,
   endpoint, type, model field) and check it still works
3. **Existing behaviour**: what worked before must still work, edge cases included
4. **Automated checks**: lint, typecheck and tests on the affected areas

Report **what was verified**, not "it should work".
If a check wasn't possible, say so explicitly.

> Verification is read-only. If a regression surfaces, **report it and stop**:
> the fix is agreed first, not applied unilaterally (Rule 3).

## Rule 5: Vet dependencies before installing

No library is installed without first checking and reporting:

- **Identity**: exact name, to rule out typosquatting (packages named almost
  identically to well-known ones)
- **Maintenance**: latest release, open issues, whether the project is alive
- **Adoption**: downloads, real-world use — an unknown package doing something
  trivial is a risk, not a convenience
- **Licence**: compatible with commercial use
- **Known vulnerabilities**: open CVEs
- **Surface**: how many transitive dependencies it pulls in

Report findings **before** installing. If the information can't be retrieved,
say so instead of assuming it's fine.
Always prefer the standard library or a dependency already present.

## Rule 6: Personal data and GDPR

This software handles real guest data, including identity documents.
Every technical decision must account for that, unprompted.

**Defaults:**
- **Minimisation**: collect and expose only what that specific feature needs.
  No `SELECT *` to the client, no fields kept "just in case"
- **No personal data in logs**: never names, emails, phone numbers, documents,
  addresses. Errors carry IDs, not values
- **Deletion and retention**: every entity holding personal data needs an answer
  to "when is this deleted?". Watch for conflicts between the right to erasure
  and retention obligations (tax, public security)
- **Identity documents**: encrypted at rest, access restricted and logged,
  never in public folders or guessable URLs
- **Access trail**: who read or modified a guest's data must be reconstructible
- **Non-production environments**: never real data. Synthetic only
- **Exports and backups**: copies of personal data, same rules apply

**Card details are never stored.** Never the number, CVV or expiry, in the
database or in logs, not even encrypted. Use the payment provider's token.
This is PCI-DSS and applies to booking guarantees too.

**If a request implies questionable processing**, flag it before implementing.
Better one extra question than one extra data point.

## Rule 7: Domain expertise

Reason like someone who knows the hospitality industry, not like someone merely
implementing a spec.

- Use correct terminology (see glossary) and keep it consistent across database,
  API and UI
- If a request conflicts with industry practice or creates a foreseeable problem,
  **say so before implementing** — then do what the user asked (Rule 3)
- On regulations, integrations and industry standards: **check the web**, don't
  rely on memory. They change, and a wrong answer here is expensive
- Never invent regulatory requirements: if unsure, say so

---

## Domain glossary

The codebase is in Italian: entities, fields, endpoints and UI text use the terms
in the "in the project" column. Keep them consistent across database, API and UI.

| Term | In the project | Meaning |
|---|---|---|
| PMS | — | the system itself (Property Management System) |
| Property | `Struttura` | the hotel/holiday home: the tenancy unit, everything hangs off it |
| Tenant / account | `Cliente` | the licence holder; owns one or more Strutture |
| Room type | `SettingTipologia` | room category: this is what gets sold |
| Room | `SettingRoom` / Camera | physical room: this is what gets assigned |
| Reservation | `Prenotazione` | has `StatoPrenotazione` and a non-reusable reservation number |
| Guest | `Ospite`, `OspiteRiga` | guest and schedina line |
| Rate plan | `GestionePrezzo`, OTA price plans | pricing applied to a room type |
| Restrictions | `RestrizioneSoggiornoCamera`, restriction plans | minimum stay, sale closures |
| Stop sale | `ChiusuraCamera` | room not sellable for a period |
| Channel manager | OTA integration (`WubookIntegrazione`) | syncs availability and rates with portals |
| OTA | sales channels | external portals (Booking, Expedia…) |
| ARI | — | Availability, Rates, Inventory: the data exchanged with channels |
| Deposit | `Cauzione` | booking deposit/guarantee |
| Revenue / expenses | `Entrata`, `Spesa` | the Struttura's cash movements |
| Invoice | `DatiFattura`, `DatiAziendali` | invoicing (PDF via QuestPDF) |
| Schedina | Alloggiati Web | guest report to the police |
| Housekeeping | `StatoCamera` | room status |
| No-show | `StatoPrenotazione` | guest never arrived |
| Overbooking | — | sold beyond availability |

## Recurring domain rules

- **Nights, not days**: a stay 10→12 is 2 nights. Check-out day excluded.
  Classic bug, check for it everywhere
- **Availability = rooms − occupied**, computed per individual date in the range,
  not on the endpoints
- **Concurrent reservations**: two requests for the last room must not both
  succeed. Needs a lock or a database-level constraint
- **Money**: never float. Decimal type or integer cents
- **Dates and time zones**: stay dates are civil dates, not timestamps.
  No UTC conversion that shifts a check-in to the previous day
- **Reservation changes**: always tracked, never overwritten without history.
  Needed for disputes and accounting

## Italian compliance

> Check the web before implementing: these change over time.

- **Alloggiati Web** — guest reporting to the police within the legal deadline
- **ISTAT** — tourism statistics reporting (regional portal)
- **Tourist tax** — municipal regulation: amount, exemptions, maximum taxable
  nights. Varies by municipality
- **E-invoicing / receipts** — SDI transmission (to be evaluated)

---

## Commands

Primary shell: **PowerShell** on Windows.

```bash
# Backend (from backend/)
dotnet build                                   # builds GestiSoft.slnx
dotnet test                                    # xUnit: Api.Tests + Application.Tests
dotnet run --project src/GestiSoft.Api         # Api on http://localhost:5080
dotnet run --project src/GestiSoft.Worker      # Quartz jobs (OTA, notifications, logs)
dotnet run --project src/GestiSoft.WorkerSchedine   # daily submissions to authorities
# lint/typecheck: no dedicated tool — compiler warnings are the check

# Frontend (from frontend/)
npm install
npm run dev        # Vite on http://localhost:5173, targets the Api on :5080
npm run build      # tsc -b && vite build
npm run lint       # oxlint
npx tsc -b         # typecheck without building

# Full stack (same command locally and in production)
docker compose up -d --build

# EF migrations (from backend/, connection string via GESTISOFT_CONNECTION_STRING)
dotnet ef migrations add <Name> --project src/GestiSoft.Infrastructure --startup-project src/GestiSoft.Api
dotnet ef database update --project src/GestiSoft.Infrastructure --startup-project src/GestiSoft.Api
```

Migrations apply themselves on Api startup (`MigrateAsync()`), production included:
`./deploy/update.sh` does `git pull` + rebuild + restart. Generating a migration is
allowed; applying one to an existing database falls under Rule 1.

## Git Configuration

- **Author**: `Gesti-Soft <gestisoftsicilia@gmail.com>`
- No AI attribution in commits (no Co-Authored-By)
- Conventional commit format: `type(scope): description`
