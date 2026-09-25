# Session report — Migrazione GestiSoft a Web

Ultimo aggiornamento: 2026-09-25. Stato: web in produzione sulla VPS, Fase 2
della sincronizzazione col desktop ferma sul ramo `fase-2-sync-locale`; su `master` tutto
committato, lavoro del 25/09 **non ancora deployato** (ricevuta di locazione breve, permessi,
pulizie, ricevuta Polizia, prezzi per occupazione e trattamenti).

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

- **Portali online PayTourist: mai provati contro un ente che li abilita davvero.** Castellammare
  non ne ha, quindi la scelta dei portali è stata verificata solo nella parte che dice "non ce ne
  sono". Da riprovare su un Comune convenzionato.
- **`HotelCode` Osservatorio: corretto ma mai verificato.** Si prova col pulsante di invio
  manuale quando il cursore del portale coincide con la data odierna: manda gli arrivi senza
  chiudere la giornata, quindi è ripetibile. Con arretrato da chiudere il manuale non fa nulla.
- **L'arrivo in una giornata passata non arriva mai all'Osservatorio**: il recupero
  dell'arretrato manda solo partenze e chiusure. Fedele al legacy, ma è un dato che non
  raggiunge una PA. Cala Azzurra ha il cursore al 15/09.
- **Migration del 25/09 applicate solo in locale**, non in produzione: `AssistenzaTicket`,
  `RicevutaLocazioneBreve`, `PermessiCamereCheckIn`, `PulizieDuranteSoggiorno`, `NotifichePulizie`,
  `FasceEtaSupplemento`, `RiduzioneOspiteInMeno`, `TipoSupplementoEuroPercentuale`, `Trattamenti`.
  `PermessiCamereCheckIn` contiene anche un `UPDATE` dei dati: da controllare prima del deploy.
- **Prezzi e trattamenti, proposte in attesa di risposta**: "Numero ospiti" → "Adulti" + bambini a
  parte (oggi chi scrive gli adulti e poi aggiunge i bambini sbaglia prezzo, riduzione compresa);
  trattamento "a persona / a camera" (l'utente si aspettava la colazione a camera); spunta "Offerto"
  automatica o avviso (un trattamento salvato con prezzo ma non offerto non compare). Il duplica-struttura
  non copia i trattamenti; il PDF dei buoni colazione non è mai stato guardato a occhio.
- **Deploy**: variabili `EMAIL_*` da aggiungere al `.env` della VPS (senza, i ticket funzionano ma
  nessuna email parte; porta 587, la 465 non è supportata). Volume nuovo `gestisoft_allegati`.
- **Ricevute modificate prima del 25/09** possono avere l'IVA nel totale: query per trovarle nei
  messaggi della sessione (`TipoEmissione = 2` con aliquota o natura valorizzate); si correggono
  riaprendole e salvando.
- **Fattura emessa al check-in e soggiorno che poi cambia**: se è già allo SdI serve una nota di
  credito, e il gestionale non guida l'operatore.
- **Da decidere**: indirizzo dell'immobile per tipologia (oggi solo per struttura); la "Dicitura in
  fattura" si stampa anche sulle ricevute; requisiti di pulizia della classificazione Regione
  Siciliana non verificati; `GestiSoft.Domain` ha entità nuove ma `PackageVersion` non alzata (il
  desktop per scelta non si aggiorna).
- **Etichette in maiuscolo spaziato** in dodici punti dell'interfaccia: stesso tic ripetuto, da
  decidere in un giro a sé.
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
- **Ragionare da esperto di hotellerie** (richiesta esplicita dell'utente): proporre la soluzione
  del settore, non solo eseguire; normativa sempre verificata sul web.
- **Permessi (25/09)**: "Esegui check-in/out" (`CheckInOut`) separato da "Stato camera"
  (`RoomStatusUpdate`, pulizie); "Pagine Camere e Tipologie" (`RoomSetupRead`) separato da "Consulta
  dati camere" (`SettingRoomRead`). L'addetto pulizie vede Check-in/out in sola lettura, riceve gli
  elenchi senza importi né canale e **non vede la pagina Ospiti** (documenti: minimizzazione GDPR).
  Qualunque cambio di stato di una camera occupata chiede "Esegui check-in/out"; nel dialogo della
  camera chiede anche conferma. Camere libere: nessun vincolo.
- **Pulizie durante il soggiorno**: pulizia e cambio biancheria sono servizi distinti. Il più
  specifico vince: rinuncia dell'ospite sulla prenotazione (registrata nel log), poi tipologia
  (vuoto = struttura, 0 = nessuna), poi struttura (vuoto = nessuna, il valore iniziale). Si conta
  dall'ultima fatta, non dalla prevista; mai il giorno dell'arrivo né quello della partenza.
  Intervalli della tipologia su un endpoint a parte: la pagina OTA rimanda il form della tipologia
  con un elenco fisso di campi e li azzererebbe.
- **"Oggi" è la data civile italiana** (`PulizieSoggiornoService.Oggi()`), non `DateTime.UtcNow.Date`
  che fino all'una/alle due di notte dà ancora ieri.
- **Notifiche riservate**: `Notifica.RichiedeStatoCamera` le mostra solo a chi ha "Stato camera".
- **Assistenza**: ticket solo per titolare e "gestione utenti"; email al Super Admin e al solo
  titolare, mai agli operatori. Foto su volume Docker (non nel database né nei backup), cancellate
  alla chiusura; ticket anonimizzati 12 mesi dopo la chiusura. Licenza scaduta: il Cliente scrive
  all'email mostrata al login, non serve aprire ticket.
- **Ricevuta di locazione breve**: marca da bollo di carta (riquadro sul PDF), mai la dicitura del
  bollo virtuale; la cedolare secca non esenta le ricevute dal bollo.
- **Prezzi per occupazione, modello Booking** (verificato sul web): prezzo della tipologia per gli ospiti
  inclusi, riduzione per ognuno in meno, supplemento per ognuno in più con fasce d'età (max 3, 0-17; 18+ o
  età fuori fascia = pieno). I posti inclusi vanno ai più grandi; i bambini contano come ospiti. In %:
  supplemento e riduzione sul prezzo della notte, fascia sul supplemento pieno (max 100%). Scelto dall'utente
  al posto di un listino per ogni occupazione. Impostazioni che non devono passare dal form generale della
  tipologia (la pagina OTA lo rimanda con campi fissi e le azzererebbe) hanno un endpoint loro.
- **Trattamenti**: listino per struttura, a persona e a notte, prezzo bambini facoltativo; salvarli richiede
  "gestione utenti" come le altre impostazioni generali. La prenotazione copia i prezzi e li ricopia solo se
  cambia il trattamento. Non vanno all'OTA.
- **Colazione e locazione breve**: colazione e pasti escludono il regime (art. 4 DL 50/2017). Se l'host incassa
  la colazione e la fa servire da un bar, l'importo è **compreso nel totale della ricevuta** senza riga propria,
  con un avviso di sentire il commercialista. **Rifiutato** di tenerlo fuori dalla ricevuta: sarebbe un
  incasso non documentato.
- **Prezzi verso l'OTA**: si inviano subito a ogni salvataggio o eliminazione di un periodo; mai prezzi a zero
  (WuBook vuole > 0,01): i giorni senza prezzo restano fuori con un avviso.

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

**Assistenza, ricevute, check-in, permessi, housekeeping** (25/09)
- **Assistenza** (commit `82267b6`): ticket con foto, pagina Cliente e pagina Super Admin, icona
  nella barra, email SMTP (`System.Net.Mail`, nessuna dipendenza nuova), nginx `/api/` a 30 MB.
- **Ricevuta**: il tipo non arrivava al frontend (si riapriva come fattura) e in modifica prendeva
  l'IVA; ora riporta soggiorno (arrivo, partenza, notti, ospiti, alloggio), immobile, pagamento,
  cedolare secca e riquadro per la marca. PDF generati e guardati con dati finti.
- **Fattura dal check-in**: dopo il check-in "Vuoi generare la fattura?" (solo con Finanze in
  scrittura); nella pagina Fatture anche i soggiorni in corso.
- **Permessi** separati e **pulizie durante il soggiorno** (sezione "Camere occupate" in Pulizie,
  14 test sulla regola), **notifica** "Pulizie di oggi" (7-14) e "di domani" (dalle 17).
- Stato camera manuale: portare in "Occupata" ha gli stessi paletti del check-in e scrive nel log.
- **Ricevuta Polizia di Stato** (commit `c3ded75`): pulsante nella pagina Polizia, metodo SOAP `Ricevuta`
  (invii di un giorno, ultimi 30 giorni escluso oggi). Al momento, non conservata, download a log.
- **Prezzo per occupazione** (commit `aaf9f79` e successivo), tutto facoltativo e solo per il preventivo
  del gestionale (le OTA calcolano il loro): età all'arrivo dei bambini sulla prenotazione
  (`EtaBambini`, almeno un adulto); fino a 3 fasce d'età 0-17 per tipologia; riduzione per ospite in meno;
  supplemento, fasce e riduzione in € o %. 20+ test sulle regole.
- **Prezzi all'OTA subito**: salvare o eliminare un periodo della tipologia lo manda a WuBook; i giorni senza
  prezzo non partono più come 0 ma si segnalano. **Mai provato contro WuBook.**
- **Trattamenti** (colazione, mezza pensione, pensione completa) in Impostazioni → Servizi, scelti nella
  prenotazione, con buoni colazione PDF (uno per ospite e mattina, niente nomi, bar convenzionato).

**Fatturazione a norma per il settore ricettivo** (17/09, seconda parte)
- Normativa verificata sul web prima di scrivere: imposta di soggiorno riaddebitata **esclusa** dalla
  base imponibile (art. 15 c.1 n.3 DPR 633/72, natura **N1** — chiarimento dell'Agenzia, non N2), bollo
  di 2 € solo sulle somme **non** soggette a IVA sopra 77,47 € (alternatività IVA/bollo, art. 6 Tabella B
  DPR 642/72), forfettari obbligati alla fattura elettronica dal 2024, locazione breve di un privato
  senza P.IVA = ricevuta non fiscale con bollo. Fonti nei messaggi della sessione.
- **Imposta di soggiorno in fattura**: riga a sé con natura N1 e riepilogo separato nell'XML, riga e
  spiegazione nel PDF, campo nel dialogo precompilato dalla prenotazione (`TotalTax`). Prima l'interfaccia
  consigliava di sommarla al prezzo del soggiorno: le faceva pagare l'IVA.
- **Imposta di bollo**: calcolata sulla sola parte non soggetta (un hotel ordinario non la paga quasi mai,
  un forfettario quasi sempre), blocco `DatiBollo` nell'XML e dicitura di legge sul PDF.
- **Ricevuta per locazione breve**: nuovo `TipoEmissione` su `DatiFattura`, **numerazione separata** dalle
  fatture (l'indice unico ora comprende la serie), niente XML né aliquota/natura, PDF con "Ricevuta" e la
  dicitura "fuori campo IVA". Nella migration il default è 1 = Fattura: con lo 0 generato da EF le fatture
  esistenti sarebbero finite in una serie fantasma e i numeri sarebbero ripartiti da 1.
- **CIN: aggiunto e poi tolto.** Era finito nel piano come voce di conformità senza verificare dove la
  norma lo pretende: obbligatorio negli annunci, all'esterno della struttura e in dichiarazione dei
  redditi (RB24/RB25, 730 rigo B12), **non** in fattura. Resta il fatto che è per unità immobiliare,
  mentre nel software ci sarebbe stato un campo solo per Struttura: Cala Azzurra ha tre appartamenti e
  presumibilmente tre CIN. Se un domani serve, va sulla Tipologia.
- Le migration del CIN restano in sequenza (crea → sposta → elimina): erano già state applicate in
  locale, e una migration applicata si annulla con un'altra, non si riscrive.
- Restano fuori: corrispettivi telematici e documento commerciale (servono un registratore telematico o
  la procedura web dell'Agenzia, non pilotabile da software terzi) e l'invio diretto allo SDI.

**PayTourist, credenziali cifrate, design, fattura estero, Osservatorio** (16-17/09, condensate: testo in archivio)
- PayTourist: portali scelti uno per uno (`paytourist_portali_attivi`), token verificato al salvataggio,
  una credenziale lasciata vuota non azzera più quella salvata. In `total_from_online_portal` va
  l'imposta già incassata dal portale (`TotalTax`).
- **Credenziali cifrate a riposo** (AES-GCM, chiave in `CREDENZIALI_CHIAVE_CIFRATURA`, fuori dal database).
  ⚠️ Senza quella variabile l'Api non parte. Cambio chiave con `..._PRECEDENTE` per un riavvio
  (`docs/deploy.md`); la chiave vecchia si conserva finché esistono backup anteriori alla rotazione.
- Design: palette fredda (l'arancione del marchio è l'unica cosa calda), importi con `stileImporto`
  e non in monospazio. Super Admin → Clienti: "Entra" sceglie il Cliente; per uscire vanno azzerati
  sia cliente sia struttura.
- Fattura estero: nazione dall'ISO2, CAP `00000`, codice destinatario `XXXXXXX`; XML non generato se
  manca un dato obbligatorio (il PDF sì). Osservatorio: `HotelCode` mancava in `<Stay>`.

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

**Fasi 0-11 — la costruzione** (dettagli in `docs/session-archive.md`)
- Fondamenta: .NET 10 + Docker + Postgres, schema dati, auth JWT con permessi per Struttura, Camere e
  Prenotazioni, Finanze e Fatturazione (PDF + XML SDI).
- Integrazioni esterne (Wubook, Alloggiati Web, Osservatorio, PayTourist) con parità rispetto a
  `OtaService.Web` del legacy; Worker diviso in due processi.
- Frontend React completo (calendario/booking board, ospiti, finanze, impostazioni, log, notifiche) e
  primo deploy reale sulla VPS.
- Migrazione dati legacy da SQL Server: una sola installazione migrata, tool rilanciabile; **i 3 utenti
  reali dell'originale non hanno un account nel nuovo sistema**, vanno ricreati a mano.
- Backup: pgBackRest (fisico + WAL, 30 giorni) e `pg_dump` (7 giorni), test di restore mensile, pagina
  "Backup" in sola lettura — il ripristino non è a un click da un pannello web, di proposito.

**Incidenti da ricordare**
- Una prenotazione reale cancellata da un'operazione di pulizia: si cancella solo ciò che
  si è creato e si sa identificare con esattezza.
- Due sessioni agent in parallelo sulla stessa cartella (vedi in cima).
