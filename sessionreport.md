# Session report — Migrazione GestiSoft a Web

Ultimo aggiornamento: 2026-09-16 (seconda parte). Stato: web in produzione sulla VPS, Fase 2
della sincronizzazione col desktop ferma sul ramo `fase-2-sync-locale`; su `master`
fatturazione (estero, PDF rifatto), Osservatorio corretto e una passata di design.

> **Cap: 300 righe.** Questo file è caricato a ogni sessione, la sua dimensione è un
> costo permanente di contesto. Voci nuove brevi: cosa è cambiato, perché, cosa resta
> aperto. Lo storico completo (dettagli di ogni sessione, punti numerati 1-581,
> verifiche) è in `docs/session-archive.md`, non caricato automaticamente.

## ⚠️ Prima di riprendere: una sola sessione per volta

Il 2026-09-15 due sessioni agent hanno ricevuto gli stessi messaggi e hanno lavorato in
parallelo sulla stessa working tree, sovrascrivendosi i file: tre entità di dominio non
tracciate da git sono state perse. Verificare che sia aperta **una sola sessione** su
questa cartella, e committare presto anche il lavoro incompleto.

## Prossima mossa — Fase 2 sync locale, dall'abbinamento in poi

Ramo **`fase-2-sync-locale`**, **non committato**: il changelog c'è e funziona, manca
tutto quello che ci sta sopra. Nell'ordine:

1. **Abbinamento dispositivo**: `/api/sync/handshake`, codice di abbinamento dal pannello
   Super Admin, token con hash su `DispositivoLocale`. L'ordine conta ed è già nel codice
   (`IStruttureConDispositivoLocale.Invalida()`): prima si dice all'intercettore che la
   struttura è abbinata, **poi** si fotografa lo stato — al contrario le modifiche fatte
   tra i due momenti non finirebbero né nella fotografia né nel changelog.
2. **Prima sincronizzazione completa**: fotografia dello stato con
   `VersioneSnapshotIniziale` valorizzata; da lì parte il primo pull.
3. **`/api/sync/pull`**: "cosa è cambiato dopo la versione N", con la proiezione delle
   integrazioni **privata dei campi credenziale** e la finestra di rilettura di 60
   secondi descritta in `SyncChange.AtUtc`.
4. **Motore di pull lato desktop** (`GestiSoft.Locale.Sync`, progetto da creare): applica
   gli upsert e avanza il segnalibro in `sync_stato`.

Resta aperto l'**installer della Fase 1** (Inno Setup + PostgreSQL portabile): rimandato
per scelta, ma finché non esiste la Fase 1 non è chiusa. Serve un PC pulito per provarlo.

## Punti aperti

Ordine non di priorità. Dettagli e design già concordati: vedi archivio.

- **`HotelCode` Osservatorio: corretto ma mai verificato.** Si prova col pulsante di invio
  manuale quando il cursore del portale coincide con la data odierna: manda gli arrivi senza
  chiudere la giornata, quindi è ripetibile. Con arretrato da chiudere il manuale non fa nulla.
- **L'arrivo in una giornata passata non arriva mai all'Osservatorio**: il recupero
  dell'arretrato manda solo partenze e chiusure. Fedele al legacy, ma è un dato che non
  raggiunge una PA. Cala Azzurra ha il cursore al 15/09.
- **Dati aziendali, campo Nazione = "Italia" invece di "IT"** (e il form lo propone così):
  finché resta, l'XML non si genera. Pulito sarebbe scrivere l'ISO2 anche per l'emittente.
- **Descrizione fattura obbligatoria solo nel dialogo**: l'Api accetta ancora vuoto, e nel
  tracciato quel campo non può esserlo.
- **PDF della fattura da rifare**, con logo della struttura: oggi non esiste nessun campo dove
  caricarlo, va deciso prima dove si conserva l'immagine.
- **Etichette in maiuscolo spaziato** in dodici punti dell'interfaccia: stesso tic ripetuto, da
  decidere in un giro a sé.
- **PDF fattura: mai guardato con gli occhi.** Si genera (provato davvero, HTTP 200 e file
  valido), ma i font incorporati impediscono di verificarne il contenuto da qui.
- **Nel PDF non c'è una riga "nazione"**: per un cliente estero lo stato si legge già
  nell'indirizzo e nel comune, come nella fattura reale accettata. Da decidere se separarla.
- **Lasciati fuori dal PDF di proposito**: unità di misura sulla riga (la quantità non sempre
  sono notti, scriverlo sarebbe falso), Pagato/Saldo (il pagato sta sulla prenotazione, altro
  registro: un saldo calcolato sottraendo i due sarebbe sbagliato), data di scadenza (dato
  inesistente, senza senso per un soggiorno saldato al check-out).
- **Dati finti sul Postgres locale da cancellare**: prenotazioni `TEST-OSSERVATORIO-03SET` e
  `-16SET` (`aaaaaaaa-…0007`/`0009`, ospiti `…0008`/`0010`), struttura Villa Chifeci Scopello.

- **Verifica licenza gestisoft.it al login + email di alert** — design deciso con
  l'utente (riuso di `WubookLicenzaService.RinnovaCredenzialiAsync`, stato per struttura
  `ok`/`scaduto`/`non configurato`/`non verificabile`, **fail-open** se il servizio non
  risponde), mai implementato.
- **Poteri Super Admin su Clienti e servizi esterni** — da riprogettare insieme, non solo
  da implementare. Include la concessione PayTourist per Comune (punto F) e la mancanza
  di un'interfaccia per creare i Comuni PayTourist.
- **Wubook per Tipologia invece che per Camera (punto B)** — parzialmente superato: il
  pooling camere identiche ha già spostato l'associazione su Tipologia. Da riverificare
  cosa resta (migration dei campi `IdCameraWubook`/`WubookAttiva`, rotte, frontend).
- **Webhook OTA diretto** (al posto del polling) — rimandato dall'utente a fine stagione.
- **`PrenotazioneDialog.tsx`**: il backend supporta già "prenota sulla Tipologia, camera
  assegnata in automatico" (`AssegnazioneCameraService`), il dialog no.
- **Colonna "Disponibilità" a 0 nella pagina Servizi OTA** — non chiarito se sia un
  problema di sincronizzazione o la lettura di un campo diverso.
- **Booking board realtime (SignalR)** — "should-have" mai fatta; il Calendario è
  comunque reattivo via React Query.
- **Vincoli di sovrapposizione date a livello di database** (`GestionePrezzo` stessa
  camera, `Prenotazione` stessa camera/periodo) e **CHECK `CheckOut > CheckIn`**: oggi
  solo validazione applicativa.
- **Calcolo automatico del Codice Fiscale** da anagrafica ospite (il legacy lo faceva):
  fatto nel form Fatturazione, non in generale sugli Ospiti.
- **XML SDI non validato contro l'XSD ufficiale**, e nessun invio reale a SDI/PEC: ci si
  ferma al file scaricabile, come il legacy.
- **Backup off-site**: pgBackRest è configurato solo in locale sulla VPS.

## ⚠️ Cosa non è mai stato verificato contro i servizi reali

Vale per **tutte** le integrazioni esterne: nessuna chiamata reale è mai partita da
questo ambiente verso Wubook, Alloggiati Web, Osservatorio Turistico o gestisoft.it.
Endpoint non configurati, nomi dei campi XML-RPC dedotti dal client legacy letto per
intero. PayTourist è l'eccezione: provato con un account di prova, 4 bug reali corretti.

Da validare prima di fidarsi in produzione: parametri `add_vplan`/`mod_vplans`/`rplan_*`,
la convenzione `TipoVariazione` (1 = importo fisso, 2 = percentuale), i codici `Type` del
Guest Osservatorio (riusano i `TipoAlloggiato` di Alloggiati Web, come il legacy).

⚠️ Le credenziali configurate in sviluppo sono quelle di **produzione** e le trasmissioni
alle PA sono irreversibili: mai inviare nulla senza richiesta esplicita.

## Decisioni in vigore

- Riscrittura completa da zero, stessa logica di business del legacy. **RabbitMQ
  eliminato** → chiamate diritte.
- **Un solo deployment multi-tenant** per tutti i clienti (decisione cambiata a metà
  Fase 0: non più un'istanza per cliente). Gerarchia Super Admin → Cliente → Struttura,
  ruoli e permessi per singola Struttura (`UtenteStruttura`). I permessi granulari, non
  il ruolo, decidono l'autorizzazione — fedele al legacy.
- Il backend licenze/abbonamenti (`GestiSoftWeb/UserService`) resta **esterno**: ci si
  integra via HTTP, non si riscrive.
- **Solo librerie ufficiali e ampiamente usate**, ogni pacchetto verificato prima di
  installarlo (richiesta esplicita dell'utente).
- Frontend con **identità visiva propria**, non il look di default delle librerie.
- **Gestione errori**: messaggi utente corretti e comprensibili ma non tecnici; il
  dettaglio va nel log, non a schermo.
- **A schermo si scrive sempre "OTA"**, mai il nome del fornitore (Wubook resta solo
  negli identificatori interni).
- **Push disponibilità verso l'OTA sempre sincrono**, a ogni creazione/modifica/
  annullamento: nessun job periodico, il rischio è l'overbooking.
- **Dominio condiviso col desktop**: `GestiSoft.Domain` si pubblica con `dotnet pack` su
  un feed a cartella (`C:\Code\GestiSoft\nuget-local`), versione attuale **1.1.0** —
  alzarla a ogni cambiamento di forma delle entità. Il locale non riceve credenziali:
  le colonne non esistono proprio nel `LocaleDbContext`.

## Pattern consolidati (riusare, non reinventare)

- Repository interface in `Application/<Modulo>/I...Repository.cs`, implementazione in
  `Infrastructure/Repositories/`, service in `Application/<Modulo>/...Service.cs`.
- Ogni servizio che riceve uno `StrutturaId` dall'esterno chiama
  `TenantAccessGuard.EnsureAccessAsync` **per primo**.
- Upsert: sempre su `db.Entry(x).State == EntityState.Detached`, mai sull'Id.
- Nuova migration: generarla, applicarla al Postgres del container, verificare con
  `psql \dt`.
- Le tabelle di riferimento (Comuni, Stati, Documenti, TipoAlloggiato) sono globali e
  condivise: dati pubblici, non dati di un cliente. Il legacy le leggeva da
  `C:\GestiSoft\Hospitality\OrderManagement\*.txt` — qui sono seed, mai path assoluti.

## Dove si trova tutto

- **Piano approvato** (fonte di verità su architettura e fasi):
  `C:\Users\Gaetano\.claude\plans\precious-dreaming-summit.md`
- **Storico completo delle sessioni**: `docs/session-archive.md`
- **Codice legacy** (sola lettura, non toccare): `C:\Code\GestiSoft\Spostare a web\`
- **Gestionale desktop offline**: `C:\Code\GestiSoft\GestiSoftGestionaleLocale`
  (repository separato, `git init` fatto, nessun commit)
- **Runbook**: `docs/deploy.md`, `docs/backup-restore.md`

## Cronologia — cosa è stato fatto

Una riga per sessione, dalla più recente. I dettagli sono nell'archivio.

**Design e pannello Super Admin** (16/09, seconda parte)
- Sfondo e bordi da caldi a freddi (`#FAF8F4` → `#F4F6F9`, `#E7E2D8` → `#E2E6EC`): il crema con
  grigi freddi sopra faceva sembrare sporca l'interfaccia, e il bordo beige contornava ogni
  riquadro. Ora l'arancione del marchio è l'unica cosa calda a schermo.
- Importi e date fuori dal monospazio: nuovo token `stileImporto` (font del testo + cifre
  tabulari). Le colonne restano incolonnate, i numeri non sembrano più un terminale. Il mono
  resta ai codici veri. Caricato anche il peso 700 del mono, che il codice chiedeva senza averlo.
- Super Admin → Clienti: barra di ricerca (cliente, struttura, partita IVA, email) e card
  rifatte. **"Entra" sceglie il Cliente** su cui operare — la select Cliente in barra è stata
  tolta, quella delle Strutture resta. Entrando, la sezione Super Admin sparisce dal menu e
  nella barra arancione compare "Torna al pannello amministratore": per uscire davvero vanno
  azzerati **sia** cliente **sia** struttura, o la prima struttura viene subito riselezionata.
- PDF della fattura rifatto: nome della struttura in testa, identità fiscale sotto in piccolo,
  documento a destra, "Fatturato a", tabella con la sola intestazione filettata, totali a destra
  col totale staccato. Niente logo (non ne esiste uno) e niente piè di pagina — regime fiscale e
  avvertenza SdI non interessano a chi riceve. Importi in euro all'italiana.
- Dati aziendali: aliquota IVA e natura predefinite, più la **dicitura di legge** da stampare
  quando l'IVA non si applica (la detta il commercialista, il software non la inventa). Due
  migrazioni, applicate al Postgres locale.
- Fattura nuova: descrizione vuota e obbligatoria, regime/aliquota/natura presi dai dati
  aziendali invece che dai valori fissi RF01 e 10%.

**Fatturazione verso l'estero e Osservatorio** (16/09)
- Stati esteri selezionabili nei campi luogo: il backend cercava già il codice tra comuni e
  stati, l'interfaccia offriva solo gli 11.283 comuni.
- Fattura elettronica estero: la sede del cliente scriveva `Nazione` fissa a "IT". Ora nazione
  dall'ISO2, CAP `00000`, provincia omessa, codice destinatario `XXXXXXX` se non ce n'è uno
  vero; nel dialogo cliente i campi dell'estero si compilano da soli. Forma copiata da una
  fattura reale già accettata: `Germania - 00000, Germania - DE`.
- Controllo prima di generare l'XML: se manca un dato obbligatorio il file non si produce e si
  dice cosa manca, invece di far scaricare qualcosa che lo SdI scarterebbe. Il PDF esce sempre.
- Osservatorio: `<Stay>` non conteneva `HotelCode` e ogni invio veniva rifiutato — il parametro
  arrivava al costruttore dell'XML e lì veniva ignorato, senza avvisi del compilatore. La
  verifica connessione non poteva accorgersene: fa login e legge la data, non costruisce Stay.
- Dati aziendali: aliquota IVA e natura predefinite, da cui parte ogni fattura nuova insieme al
  regime fiscale (prima erano fissi RF01 e 10%). Descrizione fattura vuota e obbligatoria.

**Sincronizzazione col desktop**
- Fase 1 desktop costruita e provata contro un Postgres vero (35 tabelle, `/health` su
  `127.0.0.1:5199`); pacchetto `GestiSoft.Domain` anticipato dalla Fase 2 alla Fase 1.
- Fase 2 iniziata: `SyncChange` + `DispositivoLocale` + `SyncChangeInterceptor`, che
  scrive il changelog nella stessa transazione della modifica. Costo zero finché nessuna
  struttura ha un PC abbinato. Nessuna FK verso `Struttura`, altrimenti le eliminazioni
  non arriverebbero mai al locale. Bug evitato: l'intercettore **deve** chiamare
  `ChangeTracker.DetectChanges()`, o le modifiche a righe già tracciate spariscono.
- Gestionale desktop offline progettato (architettura in `docs/architettura.md` del repo
  desktop); chiuso il buco dei doppi invii PayTourist.

**Ultime sessioni sul web**
- Il nome del fornitore OTA sparito da ogni testo a schermo.
- Schedine controllate prima dell'invio, non dopo il rifiuto del portale; termini di
  legge rispettati, invio della singola schedina, destinazione dedotta dalla camera.
- Invii alle PA: tentativi limitati con attesa crescente, niente più retry al minuto.
- Osservatorio: basta centinaia di log identici ogni sera; solo la giornata da chiudere è
  trasmissibile, letta dal servizio e non dalla cache locale.
- La scelta della Struttura del Super Admin sopravvive al ricaricamento della pagina.
- Piani prezzo OTA: percentuale ed euro erano scambiati.
- Numero prenotazione non più riassegnato, e visibile sulle dirette.
- Prenotazioni OTA: arrivi, modifiche ed errori lasciano traccia.
- Login senza "password dimenticata", e ognuno sa a chi rivolgersi.
- Sicurezza accessi: blocco dopo 5 tentativi, 2FA con app authenticator, export tracciato;
  log del Cliente limitato alla struttura selezionata.
- Fattura: nazione ISO2 dedotta dalla cittadinanza. Cassa cumulata fino all'anno scelto.

**Fase 11 — Backup e gestione DB** (2026-09-08, solo parte locale)
- pgBackRest (backup fisico + WAL, retention 30 giorni) e `pg_dump` (7 giorni), test di
  restore mensile automatico, pagina "Backup" in sola lettura per il Super Admin: il
  ripristino non è a un click da un pannello web, di proposito. Off-site ancora da fare.

**Fase 10 — Migrazione dati legacy** (SQL Server → Postgres)
- Ricognizione dei `.bak` prima di scrivere codice, ripristinati in database temporanei
  per non toccare nulla. Una sola installazione migrata; il tool è rilanciabile.
- I 3 utenti reali dell'originale **non** hanno un account nel nuovo sistema: da ricreare
  a mano.
- Importazione dati storici in una Struttura nuova via `GestiSoft.ImportStruttura`.

**Fase 9 — Design e frontend React** (completa, commit `fe03934`)
- Calendario/booking board, Ospiti, Finanze, Fatturazione, 4 schermate di integrazione,
  Utenti, Impostazioni, Log, centro notifiche in-app.
- Menu orizzontale con tab di sezione, shell mobile, select ricercabili, `CampoData` a
  segmenti, ricerca comuni per rilevanza, toast al posto degli alert.
- Dashboard Super Admin a card, statistiche per Amministratore e per Super Admin.
- Calendario: filtro Agenzia, vista Lista, colori canale stabili tra periodi.
- Pagina Servizi OTA ricostruita due volte, poi pooling delle camere identiche
  (associazione spostata da Camera a Tipologia).
- Primo deploy reale in produzione sulla VPS OVH condivisa.

**Fasi 5-8 — Integrazioni esterne**
- Wubook (channel manager), Alloggiati Web (schedine Questura), Osservatorio Turistico,
  PayTourist (imposta di soggiorno). Parità con `OtaService.Web` del legacy.
- Riduzioni tassa di soggiorno per età/residenza, verifica connessione per le tre
  integrazioni, log invii e verifiche sempre visibili.
- Worker diviso in due processi (OTA + schedine). Vincolo orario 14:00-18:00 per la
  manutenzione PayTourist.

**Fasi 0-4 — Fondamenta**
- Fase 0: scaffolding backend .NET 10 + Docker + Postgres, verificato end-to-end.
- Fase 1: schema dati operativo + seed Alloggiati Web.
- Fase 2: auth JWT, gestione errori centralizzata, Cliente/Struttura/Utente, log.
- Fase 3: Camere e Prenotazioni. Fase 4: Finanze e Fatturazione (PDF + XML SDI).

**Incidenti da ricordare**
- Una prenotazione reale cancellata da un'operazione di pulizia: si cancella solo ciò che
  si è creato e si sa identificare con esattezza.
- Due sessioni agent in parallelo sulla stessa cartella (vedi in cima).
