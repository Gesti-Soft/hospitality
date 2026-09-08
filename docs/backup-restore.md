# Backup & restore del database

Runbook in linguaggio semplice per il sistema di backup di GestiSoftGestionale. Se stai leggendo
questo perché è successo qualcosa di brutto (disco rotto, dato cancellato per errore), vai
direttamente alla sezione **"Restore di emergenza"** in fondo.

## Cosa gira in automatico, oggi

Tre task pianificati (Windows Task Scheduler, visibili in `taskschd.msc` col prefisso
`GestiSoft-`):

| Task | Quando | Cosa fa | Script |
|---|---|---|---|
| `GestiSoft-Backup-Nightly` | ogni notte alle 3:00 | Backup fisico completo (pgBackRest) + dump logico (`pg_dump`) | `docker/postgres/scripts/backup-nightly.ps1` |
| `GestiSoft-Check-Archiver` | ogni ora | Controlla che il WAL archiving non stia fallendo | `docker/postgres/scripts/check-archiver.ps1` |
| `GestiSoft-Test-Restore-Monthly` | ogni 4 settimane (domenica, 4:00) | Ripristina davvero l'ultimo backup in un ambiente usa-e-getta e verifica i dati | `docker/postgres/scripts/test-restore.ps1` |

In continuo, senza bisogno di scheduling: **Postgres archivia da solo ogni segmento WAL** appena
lo completa (`archive_command`), quindi in caso di disastro la perdita di dati è nell'ordine dei
minuti, non delle ore fino all'ultimo backup notturno (PITR — point-in-time recovery).

I log di ogni esecuzione sono in `docker/postgres/logs/` (non versionati in git, contengono solo
timestamp/esiti — nessun dato del cliente). I dump `pg_dump` sono in `docker/postgres/dumps/`
(anche questi non versionati — **contengono dati reali dei clienti**, non vanno mai committati né
condivisi).

## Come controllare che i backup siano sani

```powershell
# Stato del repository pgBackRest: ultimo backup, dimensione, range WAL coperto
docker exec -u postgres gestisoft-gestionale-postgres pgbackrest --stanza=gestisoft info

# Stato del WAL archiving in tempo reale
docker exec -u postgres gestisoft-gestionale-postgres psql -U gestisoft -d gestisoft -c "SELECT * FROM pg_stat_archiver;"
```

Controlla anche `docker/postgres/logs/check-archiver.log` (una riga OK/WARNING ogni ora) e i log
di `backup-nightly_*.log` — ogni file copre una singola esecuzione notturna.

## Retention

- **pgBackRest** (backup fisico + WAL): 30 giorni, rotazione automatica — non serve pulizia manuale.
- **pg_dump** (dump logico indipendente): 7 giorni, ripulito automaticamente dallo script notturno.

## Cosa manca ancora — copia off-site

Oggi il repository di backup (`gestisoft_pgbackrest_repo`) vive sullo **stesso PC** del database:
protegge da un dato cancellato per errore o da un backup corrotto, ma **non** da un disco/PC
distrutto o rubato — in quel caso si perderebbe sia il database sia i suoi backup insieme.

Quando si sceglie una destinazione off-site (bucket S3-compatibile tipo Backblaze B2, o un
NAS/server via SFTP), va aggiunto un secondo repository in
`docker/postgres/pgbackrest.conf`:

```ini
[global]
repo2-type=s3
repo2-s3-bucket=...
repo2-s3-endpoint=...
repo2-s3-key=...
repo2-s3-key-secret=...
repo2-retention-full-type=time
repo2-retention-full=30
```

(oppure `repo2-type=sftp` con host/utente/chiave, per un NAS/server). Solo configurazione — nessun
cambio architetturale, nessun impatto sul repo1 locale già attivo.

## Restore di emergenza

Questa è la procedura per un **vero** disastro (non il test mensile automatico, che usa un
ambiente usa-e-getta separato). Fermati e pensa prima di agire: un restore sovrascrive dati.

1. **Non toccare il volume dati originale** (`gestisoft_postgres_data`) finché non sei sicuro del
   da farsi — se il problema è "un dato è stato cancellato per errore" e non "il disco è rotto",
   valuta prima un **PITR** (restore fino a un istante preciso prima dell'errore, non l'ultimo
   backup) invece di un restore completo che perderebbe tutto ciò che è successo dopo.

2. Ferma il container Postgres reale:
   ```
   docker compose stop postgres
   ```

3. Se stai ripristinando **sullo stesso volume** (es. dati corrotti, non un disco perso): rinomina
   o rimuovi `gestisoft_postgres_data` prima di procedere (mai sovrascrivere alla cieca — se hai
   dubbi, rinominalo invece di eliminarlo, così resta recuperabile).

4. Restore completo (ultimo backup) — comando in PowerShell (usa la continuazione con backtick
   ` ` `, oppure scrivilo tutto su una riga sola):
   ```powershell
   docker run --rm -v gestisoftgestionale_gestisoft_postgres_data:/var/lib/postgresql/data `
     -v gestisoftgestionale_gestisoft_pgbackrest_repo:/var/lib/pgbackrest:ro `
     -u postgres gestisoftgestionale-postgres `
     pgbackrest --stanza=gestisoft restore
   ```

   Restore **fino a un istante preciso** (PITR, es. un minuto prima di un errore avvenuto oggi
   alle 14:32):
   ```powershell
   docker run --rm -v gestisoftgestionale_gestisoft_postgres_data:/var/lib/postgresql/data `
     -v gestisoftgestionale_gestisoft_pgbackrest_repo:/var/lib/pgbackrest:ro `
     -u postgres gestisoftgestionale-postgres `
     pgbackrest --stanza=gestisoft --type=time --target="2026-09-08 14:31:00" restore
   ```

5. Riavvia lo stack:
   ```
   docker compose up -d
   ```
   Postgres completerà da solo il replay del WAL necessario al primo avvio (stessa cosa già
   verificata nel test automatico mensile) — controlla `docker logs gestisoft-gestionale-postgres`
   fino a `database system is ready to accept connections`.

6. Verifica i dati (conteggi righe su tabelle chiave, un giro nell'app) prima di considerare il
   ripristino concluso.

**Alternativa**, se il repository pgBackRest stesso fosse inutilizzabile: i dump `pg_dump` in
`docker/postgres/dumps/` (ultimi 7 giorni) si ripristinano con `pg_restore` in un database vuoto —
meno recente, ma un formato indipendente dal repo pgBackRest.

## Cosa è stato deliberatamente lasciato fuori

- **Replica master/slave**: non prevista per questa scala (deployment singolo, non SaaS con SLA
  stringenti) — i backup con basso RPO coprono meglio il fabbisogno attuale. Da rivalutare solo con
  carico in lettura molto più alto o necessità di failover automatico.
