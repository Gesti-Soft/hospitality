# Session report — Migrazione GestiSoft a Web

Ultimo aggiornamento: 2026-09-28. Stato: web in produzione sulla VPS, Fase 2
della sincronizzazione col desktop ferma sul ramo `fase-2-sync-locale`. Su `master` il lavoro del
25/09 e del 28/09 (servizi extra, pagamenti, check-out, ordini OTA multi-camera, fattura a righe,
ticket, forfettario) è committato ma **non deployato**; il 28/09 è provato solo in parte nel browser.

> **Cap: 300 righe.** Questo file è caricato a ogni sessione, la sua dimensione è un
> costo permanente di contesto. Voci nuove brevi: cosa è cambiato, perché, cosa resta
> aperto. Lo storico completo (dettagli di ogni sessione, punti numerati 1-581,
> verifiche) è in `docs/session-archive.md`, non caricato automaticamente.

## ⚠️ Prima di riprendere: una sola sessione per volta

Il 2026-09-15 due sessioni agent hanno ricevuto gli stessi messaggi e hanno lavorato in
parallelo sulla stessa working tree, sovrascrivendosi i file: tre entità di dominio non
tracciate da git sono state perse. Verificare che sia aperta **una sola sessione** su
questa cartella, e committare presto anche il lavoro incompleto.

## Prossima mossa — finire di provare il 28/09

Committato; migration del 28/09 applicate in locale. L'utente ha provato check-out e fattura su più
prenotazioni. Restano da provare: "Fattura gli extra" e la domanda al check-out, ticket dei trattamenti,
forfettario (N2.2 automatico), Cittadinanza a select nel cliente, PDF/XML della fattura a righe.
Regola fattura: aliquota > 0 senza natura, oppure **0% con la natura**. Proposte in attesa di risposta:
colonna "Tipo" (Fattura/Ricevuta) nell'elenco fatture; dicitura del forfettario proposta in automatico;
"Genera fattura" da Storico/In corso/check-out con le altre prenotazioni aggiungibili.

## Poi — Fase 2 sync locale, dall'abbinamento in poi

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
- **Migration del 28/09 applicate solo in locale**: `NoteOtaPrenotazione`,
  `ServiziExtraAllInclusive` (inserisce i 4 trattamenti per le strutture esistenti), `ServiziExtraDate`,
  `PrenotazioneOtaPiuCamere`, `RegistroPagamenti` (converte ogni "Importo pagato" in un pagamento: su
  locale 4.078 righe, totale identico), `FatturaARighe` (copia la riga unica come riga 1, **poi** toglie le
  colonne: l'ordine è stato corretto a mano, EF le toglieva prima), `TrattamentoStampaTicket`.
- **XML fattura con imposta di soggiorno: IVA sbagliata in produzione fino al 28/09** (contava anche
  l'imposta: 200 € al 10% + 12 € davano IVA 32). Corretto; da contare in produzione le fatture con
  `ImpostaSoggiorno > 0` il cui XML è già stato scaricato/inviato. Nello stesso XML aliquota scritta "10" e
  non "10.00", AliquotaIVA assente con la sola natura: corretti nella fattura a righe.
- **Trattamenti, proposte in attesa di risposta**: "a persona / a camera" (l'utente si aspettava la
  colazione a camera); spunta "Offerto" automatica o avviso. Il duplica-struttura non copia trattamenti
  né servizi extra; il PDF dei buoni colazione non è mai stato guardato a occhio.
- Ordini OTA multi-camera e trattamento/note da OTA mai provati contro WuBook (campi dalla documentazione). Ordini multi-camera persi prima del 28/09:
  nel log come "Id camera OTA non numerico", da reinserire a mano.
- **Note OTA senza scadenza**: possono contenere dati di salute (allergie); proposto di svuotarle
  qualche settimana dopo il check-out, non deciso. Le date dei servizi non seguono le modifiche OTA.
- **Da verificare col commercialista**: aliquota dei servizi extra (SPA/parcheggio 10% se accessori, 22%
  se autonomi), escursioni di terzi (art. 74-ter), servizi extra nella ricevuta di locazione breve.
- **Deploy**: variabili `EMAIL_*` da aggiungere al `.env` della VPS (senza, i ticket funzionano ma
  nessuna email parte; porta 587, la 465 non è supportata). Volume nuovo `gestisoft_allegati`.
- **Ricevute modificate prima del 25/09** possono avere l'IVA nel totale: query per trovarle nei
  messaggi della sessione (`TipoEmissione = 2` con aliquota o natura valorizzate); si correggono
  riaprendole e salvando.
- **Fattura emessa al check-in e soggiorno che poi cambia**: se è già allo SdI serve una nota di
  credito, e il gestionale non guida l'operatore (TD04 selezionabile ma senza collegamento alla fattura
  originale né storno). Gli extra addebitati dopo si fatturano in un secondo documento.
- **Da decidere**: indirizzo dell'immobile per tipologia (oggi solo per struttura); la "Dicitura in
  fattura" si stampa anche sulle ricevute; requisiti di pulizia della classificazione Regione
  Siciliana non verificati; `GestiSoft.Domain` ha entità nuove ma `PackageVersion` non alzata (il
  desktop per scelta non si aggiorna).
- **Etichette in maiuscolo spaziato** in dodici punti dell'interfaccia: da decidere in un giro a sé.
- **Nel PDF non c'è una riga "nazione"** (per l'estero sta nell'indirizzo): da decidere se separarla.
- **Lasciati fuori dal PDF di proposito**: unità di misura sulla riga, Pagato/Saldo (il pagato sta
  sul registro della prenotazione), data di scadenza (senza senso per un soggiorno saldato al check-out).
- **Dati finti sul Postgres locale da cancellare**: prenotazioni `TEST-OSSERVATORIO-03SET` e
  `-16SET` (`aaaaaaaa-…0007`/`0009`, ospiti `…0008`/`0010`), struttura Villa Chifeci Scopello. Su Villa
  Chifeci: #37-#40 (`eeeeeeee-0000-4000-8000-…0001`-`0004`, ospiti `…0011`-`0014`, servizi `…0021`-`0024`,
  prima le fatture fatte su di loro); #36 con date e stato cambiati a mano (era 28→29/09, Incompleta).
- **Anno e data della fattura in UTC** (`FatturazioneService`, `DateTime.UtcNow`): a Capodanno tra 00 e 01
  la fattura prende l'anno prima. Da correggere con la data civile italiana, proposto, non fatto.

- **Verifica licenza gestisoft.it al login + email di alert** — design deciso con
  l'utente (riuso di `WubookLicenzaService.RinnovaCredenzialiAsync`, stato per struttura
  `ok`/`scaduto`/`non configurato`/`non verificabile`, **fail-open** se il servizio non
  risponde), mai implementato.
- **Poteri Super Admin su Clienti e servizi esterni** — da riprogettare insieme, non solo
  da implementare. Include la concessione PayTourist per Comune (punto F) e la mancanza
  di un'interfaccia per creare i Comuni PayTourist.
- **Wubook per Tipologia (punto B)**: in parte superato dal pooling; da riverificare campi
  `IdCameraWubook`/`WubookAttiva`, rotte, frontend. **Webhook OTA** rimandato a fine stagione.
- **Colonna "Disponibilità" a 0 nella pagina Servizi OTA**: causa non chiarita.
- **Booking board realtime (SignalR)** mai fatta (il Calendario è reattivo via React Query).
- **Vincoli di sovrapposizione date nel database** (`GestionePrezzo`, `Prenotazione`) e **CHECK
  `CheckOut > CheckIn`**: oggi solo validazione applicativa.
- **Codice Fiscale automatico**: solo nel form Fatturazione, non sugli Ospiti.
- **XML SDI non validato contro l'XSD**, nessun invio reale a SDI/PEC (file scaricabile, come il legacy).
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
- **Fiscale (17/09, testo in archivio)**: imposta di soggiorno riaddebitata **esclusa** dall'imponibile, natura
  **N1** (art. 15), riga e riepilogo a sé; bollo 2 € solo sulla parte **non** soggetta sopra 77,47 €; ricevuta di
  locazione breve con numerazione separata. ⚠️ Credenziali cifrate: senza `CREDENZIALI_CHIAVE_CIFRATURA` l'Api
  non parte (rotazione in `docs/deploy.md`).
- **Ospiti (28/09)**: nel dialogo "Adulti" + bambini con età, che si **aggiungono**; `NumeroOspiti` resta il totale.
- **Trattamenti e servizi (28/09)**: colazione, mezza pensione, pensione completa, all inclusive creati per ogni
  struttura, non offerti, non cancellabili; **uno** per prenotazione. Servizi extra liberi, più d'uno, cancellazione
  = nascosti (le prenotazioni che li hanno venduti li tengono). Riga con data (a notte: dal–al come check-in/out),
  prezzo copiato alla vendita, origine "con la prenotazione" / "durante il soggiorno" (dopo il check-in, anche
  dopo il check-out) e autore. Aliquota per servizio, vuota = quella della struttura. Tab "Trattamento e servizi"
  solo se c'è qualcosa di offerto.
- **Dall'OTA comanda la prenotazione**: trattamento riconosciuto (boards, extra, testo esplicito, mai le richieste
  dell'ospite) sovrascrive; se l'OTA non dice nulla resta quello che c'è. Note OTA salvate senza dati di carta,
  mai nei log né all'addetto pulizie. Ordine con più camere = una prenotazione per camera, stesso rcode +
  `IndiceCameraOta`; chi ha prenotato solo sulla camera 1.
- **Pagamenti (28/09)**: registro con data, tipo (acconto, caparra, saldo, altro, rimborso) e metodo; `ImportoPagato`
  ne è la somma e non si scrive più a mano. **Cassa per data del pagamento** (scelta dell'utente). Annullare **non
  azzera** i soldi ricevuti: si registra un rimborso. Permessi come prima (modifica prenotazione).
- **Check-out** con conto e saldo registrabile; si può chiudere con saldo aperto dopo averlo visto; poi proposta
  di fattura se non ancora fatturata. "Addebita" rapido da Check-in/out somma l'importo al totale.
- **Fattura a righe (28/09)**: IVA per riga, riepilogo e IVA calcolati **per aliquota** (`CalcoloFattura`, unica fonte
  per servizio, PDF, XML). Più prenotazioni per documento, la prima è di chi paga. Soggiorno e ogni servizio extra
  fatturabili **una volta sola**. Proposta con **IVA in aggiunta** al prezzo della prenotazione (scelta dell'utente,
  come prima) e **senza la cauzione** (deposito, non corrispettivo). "Nuova fattura" elenca solo prenotazioni
  senza documento; gli extra addebitati dopo vanno in un **secondo documento** ("Fattura gli extra" sulla
  prenotazione e domanda al check-out), mai riaprendo la prima.
- **Forfettario (RF19) e minimi (RF02)**: ogni riga 0% **N2.2** in automatico, anche sui servizi con aliquota
  propria; il backend rifiuta righe con IVA (verificato sul web, Agenzia Entrate).
- **Ticket dei trattamenti**: spunta "Stampa ticket" sui quattro trattamenti fissi (niente trattamenti creati
  dall'utente, scelta sua); l'OTA non la tocca. Colazione: mattine dopo le notti; gli altri: giorni delle notti.

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

**Prenotazioni, extra, pagamenti, fatture** (28/09)
- Dialogo prenotazione a tab (Soggiorno / Trattamento e servizi / Pagamenti), importi fissi in basso, assegnazione
  automatica con preventivo e avviso di tipologia piena (`camera-assegnabile`), "Adulti" separati dai bambini.
- Trattamento e note dall'OTA; all inclusive; servizi extra con listino, date e addebiti; ordini OTA multi-camera
  (prima persi). Registro pagamenti, check-out con saldo, "Addebita" e sezione "In struttura" in Check-in/out.
- Bug corretti: IVA dell'XML con imposta di soggiorno; aliquota "10" → "10.00"; AliquotaIVA mancante con la natura.
- Fattura a righe e multi-prenotazione, aliquota IVA sui servizi extra. 269 test verdi.
- Poi: ticket sui trattamenti, N2.2 automatico per il forfettario, "Fattura gli extra" (`da-fatturare`), etichetta
  Fattura/Ricevuta anche nel dialogo prenotazione, cliente con Cittadinanza a select, spaziatura dei tab.

**Assistenza, ricevute, check-in, permessi, housekeeping, prezzi** (25/09, commit `82267b6`…`c559e57`)
- Ticket di assistenza con foto ed email SMTP; ricevuta di locazione breve completa (tipo, soggiorno,
  immobile, pagamento, cedolare, marca); "Vuoi generare la fattura?" dopo il check-in.
- Permessi separati, pulizie durante il soggiorno (14 test) e notifiche pulizie; stato camera manuale
  con i paletti del check-in.
- Ricevuta della Polizia di Stato (SOAP `Ricevuta`, ultimi 30 giorni escluso oggi, non conservata).
- Prezzo per occupazione (età bambini, fasce, riduzione, € o %; 20+ test); prezzi all'OTA a ogni
  salvataggio (**mai provato contro WuBook**); trattamenti con buoni colazione PDF.

**Fatturazione a norma, PayTourist, credenziali cifrate, ultime sessioni di settembre** (fino al 17/09):
testo in `docs/session-archive.md`, voci spostate il 28/09. Le decisioni ancora in vigore sono sopra.

**Sincronizzazione col desktop**
- Fase 1 desktop costruita e provata contro un Postgres vero; pacchetto `GestiSoft.Domain` anticipato.
- Fase 2 iniziata: `SyncChange` + `DispositivoLocale` + `SyncChangeInterceptor` (changelog nella stessa
  transazione, nessuna FK verso `Struttura`; l'intercettore **deve** chiamare `DetectChanges()`).

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
