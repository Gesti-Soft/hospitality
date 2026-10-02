using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Notifiche;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Application.Servizi;
using GestiSoft.Application.Trattamenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Wubook;

public record RisultatoSincronizzazionePrenotazioni(int Importate, int Aggiornate, int Annullate, int Errori);

/// <summary>
/// Pull prenotazioni da Wubook — porta OtaService.ComunicationLogic.FetchNewBooking +
/// OrderManagement.PrenotationLogic.FetchBooking del legacy (vedi report Fase 5 sez. C), con due
/// bug noti del legacy corretti qui:
/// 1. fetch_new_bookings veniva letto come singola prenotazione invece che come array — qui
///    <see cref="IWubookClient.FetchNewBookingsAsync"/> ritorna la lista completa.
/// 2. la deduplica per NumeroPrenotazione (stringa, collidibile tra canali) è sostituita da
///    IdPrenotazioneWubook (l'rcode Wubook, univoco, con vincolo unique a DB — vedi Prenotazione).
/// </summary>
public class WubookPrenotazioniService(
    IPrenotazioneRepository prenotazioni,
    IOspiteRepository ospiti,
    ITipologiaCameraRepository tipologie,
    ICameraRepository camere,
    AssegnazioneCameraService assegnazioneCamera,
    ICanaleVenditaRepository canaliVendita,
    IWubookClient wubookClient,
    WubookLicenzaService licenzaService,
    IStrutturaRepository strutture,
    PermessoStrutturaGuard permessoGuard,
    ILogEventoService logEventi,
    NotificaService notificaService,
    ITrattamentoStrutturaRepository listiniTrattamento,
    IServizioStrutturaRepository serviziStruttura)
{
    private enum EsitoBooking { Creata, Aggiornata, Annullata, Ignorata }

    /// <summary>Entro quante ore dalla creazione ha ancora senso segnalare come "arrivata" una prenotazione la cui notifica non era mai stata creata (vedi il recupero in ImportaBookingAsync).</summary>
    private const int OreRecuperoArrivoNonNotificato = 24;

    public async Task<RisultatoSincronizzazionePrenotazioni> SincronizzaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);
        // Con la ricezione diretta fetch_new_bookings non si usa (vedi WubookAvvisiDirettiService): dall'Api
        // farebbe anche concorrenza al Worker, che potrebbe importare la stessa prenotazione nello stesso momento.
        if (await licenzaService.AvvisiDirettiAttiviAsync(strutturaId, cancellationToken))
        {
            throw new ConflictException("Le prenotazioni dell'OTA arrivano già in automatico appena fatte: non serve sincronizzarle a mano.");
        }

        return await SincronizzaSistemaAsync(strutturaId, cancellationToken);
    }

    /// <summary>Usato dal job Quartz schedulato (Worker) — nessun ICurrentUser, gira per conto del sistema per ogni struttura attiva.</summary>
    public async Task<RisultatoSincronizzazionePrenotazioni> SincronizzaSistemaAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        var canali = await wubookClient.GetChannelsInfoAsync(token, cancellationToken);
        var prenotazioniWubook = await wubookClient.FetchNewBookingsAsync(token, lcode, cancellationToken);

        int importate = 0, aggiornate = 0, annullate = 0, errori = 0;
        foreach (var booking in prenotazioniWubook)
        {
            try
            {
                var nomeCanale = canali.FirstOrDefault(c => c.Id == booking.IdChannel)?.Nome ?? "Sito Web";
                var esito = await ImportaBookingAsync(strutturaId, booking, nomeCanale, cancellationToken);
                switch (esito)
                {
                    case EsitoBooking.Creata: importate++; break;
                    case EsitoBooking.Aggiornata: aggiornate++; break;
                    case EsitoBooking.Annullata: annullate++; break;
                }
            }
            catch (Exception ex)
            {
                errori++;
                await logEventi.RegistraAsync(
                    LivelloLog.Warning,
                    $"Import prenotazione OTA rcode={booking.RCode}: {ex.Message}",
                    origine: "Wubook",
                    clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                    strutturaId: strutturaId,
                    categoria: "Wubook",
                    cancellationToken: cancellationToken);
            }
        }

        return new RisultatoSincronizzazionePrenotazioni(importate, aggiornate, annullate, errori);
    }

    /// <summary>Elabora una singola prenotazione Wubook già ottenuta (fetch_booking o fetch_new_bookings) — riusato anche da WubookEventiService per il polling minute-by-minute.</summary>
    public async Task ImportaBookingRicevutoAsync(Guid strutturaId, WubookPrenotazione booking, string nomeCanale, CancellationToken cancellationToken) =>
        await ImportaBookingAsync(strutturaId, booking, nomeCanale, cancellationToken);

    /// <summary>
    /// Per il controllo periodico (vedi WubookAvvisiDirettiService): importa la prenotazione solo se
    /// qui non c'è ancora, o se l'OTA la dà cancellata e qui è ancora attiva. Una prenotazione già
    /// presente non si reimporta mai: l'import riscrive importo e dati con quelli dell'OTA, e ogni
    /// controllo cancellerebbe le correzioni fatte nel gestionale (addebiti, camera spostata…).
    /// Le modifiche arrivano con l'avviso dell'OTA. Ritorna true se ha importato qualcosa.
    /// </summary>
    public async Task<bool> ImportaSeMancanteAsync(Guid strutturaId, WubookPrenotazione booking, string nomeCanale, CancellationToken cancellationToken)
    {
        var esistenti = await prenotazioni.ListByIdPrenotazioneWubookAsync(strutturaId, booking.RCode, cancellationToken);
        var cancellata = booking.Status == 5;
        var daImportare = esistenti.Count == 0
            ? !cancellata
            : cancellata && esistenti.Any(p => p.StatoPrenotazione != StatoPrenotazione.Annullata);
        if (!daImportare)
        {
            return false;
        }

        await ImportaBookingAsync(strutturaId, booking, nomeCanale, cancellationToken);
        return true;
    }

    /// <summary>
    /// Un ordine OTA diventa una prenotazione per camera (vedi OrdineOta), tutte con lo stesso rcode.
    /// Le tipologie si risolvono tutte prima di scrivere: un ordine con una camera non associata non
    /// deve restare registrato a metà.
    /// </summary>
    private async Task<EsitoBooking> ImportaBookingAsync(Guid strutturaId, WubookPrenotazione booking, string nomeCanale, CancellationToken cancellationToken)
    {
        var camereOrdine = OrdineOta.Suddividi(booking);
        var esistenti = await prenotazioni.ListByIdPrenotazioneWubookAsync(strutturaId, booking.RCode, cancellationToken);

        // Status 5 = prenotazione cancellata lato Wubook (vedi report Fase 5 sez. C).
        if (booking.Status == 5)
        {
            var annullate = 0;
            foreach (var esistente in esistenti)
            {
                if (await AnnullaDaOtaAsync(strutturaId, esistente, booking.RCode, nomeCanale, "annullata dall'OTA", cancellationToken))
                {
                    annullate++;
                }
            }

            return annullate > 0 ? EsitoBooking.Annullata : EsitoBooking.Ignorata;
        }

        var tipologieCamere = new List<SettingTipologia>();
        foreach (var camera in camereOrdine)
        {
            tipologieCamere.Add(await tipologie.GetByIdWubookAsync(strutturaId, camera.IdCameraWubook, cancellationToken)
                ?? throw new InvalidOperationException($"Nessuna tipologia locale associata alla camera OTA {camera.IdCameraWubook}."));
        }

        var esito = EsitoBooking.Aggiornata;
        foreach (var camera in camereOrdine)
        {
            var esitoCamera = await ImportaCameraAsync(
                strutturaId, booking, camera, camereOrdine.Count, tipologieCamere[camera.Indice],
                esistenti.FirstOrDefault(p => p.IndiceCameraOta == camera.Indice), nomeCanale, cancellationToken);
            if (esitoCamera == EsitoBooking.Creata && camera.Indice == 0)
            {
                esito = EsitoBooking.Creata;
            }
        }

        // L'ospite ha tolto una camera dall'ordine sul portale: quella in più si annulla come una cancellazione.
        foreach (var tolta in esistenti.Where(p => p.IndiceCameraOta >= camereOrdine.Count))
        {
            await AnnullaDaOtaAsync(strutturaId, tolta, booking.RCode, nomeCanale, "tolta dall'ordine dall'OTA", cancellationToken);
        }

        return esito;
    }

    /// <summary>
    /// Annulla una prenotazione per conto dell'OTA. Una già in corso (check-in fatto) non si tocca:
    /// si segnala per una verifica manuale. Restituisce true se l'ha annullata.
    /// </summary>
    private async Task<bool> AnnullaDaOtaAsync(Guid strutturaId, Prenotazione esistente, int rcode, string nomeCanale, string motivo, CancellationToken cancellationToken)
    {
        if (esistente.StatoPrenotazione == StatoPrenotazione.Annullata)
        {
            return false;
        }

        // Se l'ospite ha già fatto check-in (camera occupata, presenza reale in struttura), una
        // cancellazione OTA tardiva non va applicata in automatico: importi/stato potrebbero non
        // riflettere più la realtà (es. saldo incassato in loco). Si lascia intatta e si segnala
        // per una verifica manuale, invece di annullare/azzerare silenziosamente.
        if (esistente.StatoPrenotazione == StatoPrenotazione.InCorso)
        {
            var numeroVisualizzatoInCorso = esistente.NumeroPrenotazione ?? esistente.Id.ToString()[..8];
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                $"L'OTA segnala come cancellata la prenotazione #{numeroVisualizzatoInCorso} (rcode={rcode}), ma risulta già In corso (check-in effettuato): nessuna modifica automatica, verificare manualmente.",
                origine: "Wubook",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "Wubook",
                cancellationToken: cancellationToken);
            await notificaService.CreaPerPrenotazioneSeNonEsisteAsync(
                strutturaId, TipoNotifica.PrenotazioneAnnullata, esistente.Id,
                "Cancellazione da verificare",
                $"L'OTA segnala come cancellata la prenotazione #{numeroVisualizzatoInCorso}, ma l'ospite ha già fatto check-in: verificare manualmente.",
                cancellationToken);
            return false;
        }

        // Arrivati qui la prenotazione non era In corso (vedi controllo sopra), quindi il
        // check-in non è mai avvenuto e la camera non è mai stata toccata da questa prenotazione
        // — non c'è nulla da liberare. Se la camera risulta occupata/non pronta, è per un motivo
        // indipendente (altro soggiorno in corso, blocco manuale) e non va alterato qui.
        var numeroVisualizzato = esistente.NumeroPrenotazione ?? esistente.Id.ToString()[..8];
        var canaleCancellata = esistente.Agenzia ?? nomeCanale;

        esistente.StatoPrenotazione = StatoPrenotazione.Annullata;
        // Come l'annullamento a mano: i soldi già ricevuti restano, un eventuale rimborso si registra.
        esistente.ImportoPrenotazione = 0;
        esistente.ImportoTotale = 0;
        // L'imposta di soggiorno è dovuta per i pernottamenti: senza soggiorno non c'è.
        esistente.TotalTax = 0;
        esistente.UpdatedAtUtc = DateTime.UtcNow;
        await prenotazioni.UpdateAsync(esistente, cancellationToken);

        await logEventi.RegistraAsync(
            LivelloLog.Info,
            $"Prenotazione #{numeroVisualizzato}{DescriviCamera(esistente.IndiceCameraOta)} {motivo} (rcode={rcode}).",
            origine: "Wubook",
            clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
            strutturaId: strutturaId,
            categoria: "Wubook",
            cancellationToken: cancellationToken);

        // Non ancora una notifica visibile: Wubook, quando un operatore modifica una prenotazione
        // da un canale OTA, manda la cancellazione del vecchio rcode e subito dopo una nuova
        // prenotazione con i dati aggiornati — resta InAttesa per una breve finestra di grazia, in
        // modo da poterla fondere con quella nuova (vedi RegistraNuovaOModificaWubookAsync) invece
        // di notificare due volte la stessa modifica. Solo per la prima camera dell'ordine: è quella
        // con l'ospite, e la fusione si fa sull'ospite.
        if (esistente.IndiceCameraOta == 0)
        {
            await notificaService.RegistraCancellazioneWubookAsync(
                strutturaId, esistente.Id, canaleCancellata,
                "Prenotazione cancellata",
                $"Prenotazione #{numeroVisualizzato} ({canaleCancellata}) cancellata dall'OTA.",
                cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Note della prenotazione: in un ordine con più camere, prima di tutto a quale ordine appartiene,
    /// così dalla seconda camera si ritrova la prima (e chi ha prenotato) e si sa che gli ospiti vanno
    /// inseriti a parte. Poi quello che l'OTA ha mandato, uguale per tutte le camere.
    /// </summary>
    private static string? ComponiNoteCamera(WubookPrenotazione booking, CameraOrdineRisolta camera, int camereNellOrdine)
    {
        var note = TrattamentoOta.ComponiNote(booking.Extra);
        if (camereNellOrdine <= 1)
        {
            return note;
        }

        var ordine = !string.IsNullOrWhiteSpace(booking.ChannelReservationCode) ? booking.ChannelReservationCode : booking.RCode.ToString();
        var intestazione = camera.Indice == 0
            ? $"Camera 1 di {camereNellOrdine} dell'ordine #{ordine}: le altre camere sono prenotazioni a parte con lo stesso numero."
            : $"Camera {camera.Indice + 1} di {camereNellOrdine} dell'ordine #{ordine}: chi ha prenotato è sulla camera 1, gli ospiti di questa camera vanno inseriti a parte.";
        var testo = note is null ? intestazione : $"{intestazione}\n{note}";
        return testo.Length <= TrattamentoOta.LunghezzaMassimaNote ? testo : testo[..(TrattamentoOta.LunghezzaMassimaNote - 1)] + "…";
    }

    /// <summary>" (camera 2)" per le camere dopo la prima di un ordine, niente per la prima.</summary>
    private static string DescriviCamera(int indice) => indice > 0 ? $" (camera {indice + 1})" : "";

    private async Task<EsitoBooking> ImportaCameraAsync(
        Guid strutturaId,
        WubookPrenotazione booking,
        CameraOrdineRisolta camera,
        int camereNellOrdine,
        SettingTipologia tipologia,
        Prenotazione? esistente,
        string nomeCanale,
        CancellationToken cancellationToken)
    {
        // Nel log e nelle notifiche: "(camera 2 di 3)" solo se l'ordine ne ha più d'una.
        var dellOrdine = camereNellOrdine > 1 ? $" (camera {camera.Indice + 1} di {camereNellOrdine})" : "";

        var nuova = esistente is null;
        var entity = esistente ?? new Prenotazione
        {
            StrutturaId = strutturaId,
            IdPrenotazioneWubook = booking.RCode,
            IndiceCameraOta = camera.Indice,
            StatoPrenotazione = StatoPrenotazione.Incompleta,
        };

        // Stato prima dell'aggiornamento, per riconoscere se un booking già noto (stesso rcode) è
        // cambiato davvero: Wubook riusa lo stesso rcode quando l'ospite modifica date/importo/
        // persone da OTA senza passare da cancella+ricrea, e senza questo confronto la modifica
        // verrebbe scritta sul database in silenzio — nessuna notifica, nessun log (vedi sotto).
        var primaCheckIn = entity.CheckIn;
        var primaCheckOut = entity.CheckOut;
        var primaImporto = entity.ImportoPrenotazione;
        var primaNumeroOspiti = entity.NumeroOspiti;
        var primaTipologiaId = entity.TipologiaId;
        var primaCameraId = entity.CameraId;
        var primaTrattamento = entity.Trattamento;

        // Non riassegnare se la camera già assegnata in precedenza (un aggiornamento di date su un
        // booking esistente) resta compatibile con le nuove date — evita di spostare inutilmente un
        // ospite già assegnato a una camera del pool. Deve però appartenere ancora alla Tipologia
        // risolta sopra: se nel frattempo la mappatura OTA→Tipologia è cambiata, tenere la vecchia
        // camera per sole date libere assegnerebbe una camera del pool sbagliato (stesso bug corretto
        // in PrenotazioniService.AggiornaAsync). Se il pool non ha nessuna unità libera per queste
        // date, la prenotazione viene comunque registrata (l'ospite ha già prenotato per davvero su
        // OTA, non va persa) con CameraId nullo: resta "in attesa di assegnazione camera" finché un
        // operatore non gliene assegna una manualmente.
        var cameraAttuale = entity.CameraId is { } cameraIdAttuale ? await camere.GetAsync(cameraIdAttuale, cancellationToken) : null;
        var cameraAttualeRestaValida = cameraAttuale is not null
            && cameraAttuale.TipologiaId == tipologia.Id
            && !await prenotazioni.EsisteSovrapposizioneAsync(strutturaId, cameraAttuale.Id, booking.CheckIn, booking.CheckOut, entity.Id, cancellationToken);
        if (!cameraAttualeRestaValida)
        {
            var cameraLibera = await assegnazioneCamera.TrovaCameraLiberaAsync(strutturaId, tipologia.Id, booking.CheckIn, booking.CheckOut, entity.Id, cancellationToken);
            entity.CameraId = cameraLibera?.Id;
            if (cameraLibera is null)
            {
                var numeroVisualizzatoSenzaCamera = entity.NumeroPrenotazione ?? booking.RCode.ToString();
                await logEventi.RegistraAsync(
                    LivelloLog.Warning,
                    $"Prenotazione #{numeroVisualizzatoSenzaCamera}{dellOrdine} da {nomeCanale} (rcode={booking.RCode}): nessuna camera libera nel pool '{tipologia.TipologiaCamera}' per {booking.CheckIn:dd/MM/yyyy}–{booking.CheckOut:dd/MM/yyyy} — registrata senza camera assegnata, serve assegnazione manuale.",
                    origine: "Wubook",
                    clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                    strutturaId: strutturaId,
                    categoria: "Wubook",
                    cancellationToken: cancellationToken);
            }
        }

        entity.TipologiaId = tipologia.Id;
        entity.Agenzia = nomeCanale;
        await AssicuraCanaleVenditaAsync(strutturaId, nomeCanale, cancellationToken);
        entity.NumeroPrenotazione = !string.IsNullOrWhiteSpace(booking.ChannelReservationCode) ? booking.ChannelReservationCode : booking.RCode.ToString();
        entity.ImportoPrenotazione = camera.Importo;
        entity.ImportoTotale = camera.Importo;
        entity.CheckIn = booking.CheckIn;
        entity.CheckOut = booking.CheckOut;
        entity.NumeroOspiti = camera.NumeroOspiti;
        entity.Anno = booking.CheckIn.Year;
        entity.NoteOta = ComponiNoteCamera(booking, camera, camereNellOrdine);
        // Dichiarato sul sito web: comanda la prenotazione, come il trattamento. Dai portali il dato
        // non arriva e resta quello che c'è. Il totale non cambia: è già quello pagato sul sito.
        if (ServiziSitoWeb.Animale(booking.Extra) is { } animale)
        {
            entity.AnimaliAttiva = animale;
        }
        // L'ha comprato l'ospite sul portale: comanda l'OTA. Se però non dice nulla di riconoscibile
        // (il caso più comune) resta quello che c'è, per esempio una colazione venduta al banco.
        var trattamentoOta = TrattamentoOta.Riconosci(booking.Extra);
        // Venduto dal sito web: i prezzi pagati arrivano con la prenotazione e valgono al posto del
        // listino, anche quando cambiano a parità di trattamento.
        var prezziSito = trattamentoOta.Trattamento is not null ? ServiziSitoWeb.LeggiPrezziTrattamento(booking.Extra) : null;
        if (trattamentoOta.Indicato
            && (trattamentoOta.Trattamento != entity.Trattamento
                || prezziSito is not null && prezziSito != TrattamentiService.PrezziDellaPrenotazione(entity)))
        {
            await ImpostaTrattamentoOtaAsync(strutturaId, entity, trattamentoOta.Trattamento, prezziSito, nomeCanale, booking.RCode, cancellationToken);
        }
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (nuova)
        {
            // Stessa regola di PrenotazioniService.CreaAsync: una prenotazione (qui arrivata da OTA,
            // non creata a mano) non deve mai comparire come "da inviare" per un servizio che la
            // Struttura non ha (es. niente account PayTourist -> PayTourist già marcato "inviato").
            // TassaSoggiornoAttiva resta al default true (Wubook non espone questo concetto), quindi
            // qui conta solo se il servizio è concesso.
            var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken);
            entity.StatePolice = struttura is null || !struttura.AlloggiatiWebAbilitato;
            entity.PMS = struttura is null || !struttura.OsservatorioAbilitato;
            entity.PayTourist = struttura is null || !struttura.PayTouristAbilitato;
            await prenotazioni.AddAsync(entity, cancellationToken);
            await ImportaServiziSitoAsync(strutturaId, entity, booking, camera.Indice, nomeCanale, cancellationToken);

            // L'arrivo di una prenotazione da OTA va sempre a log, non solo quando qualcosa va
            // storto: prima era l'unico evento Wubook a non lasciare traccia (cancellazione e
            // fallimenti sì, l'import riuscito no), quindi chi non aveva visto passare la notifica
            // non aveva più nessun modo di sapere quando e da dove fosse arrivata.
            await logEventi.RegistraAsync(
                LivelloLog.Info,
                $"Nuova prenotazione #{entity.NumeroPrenotazione}{dellOrdine} da {nomeCanale} (rcode={booking.RCode}): {booking.CheckIn:dd/MM/yyyy}–{booking.CheckOut:dd/MM/yyyy}, {entity.NumeroOspiti} ospiti, {camera.Importo:N2} €{(entity.Trattamento is { } trattamento ? $", {TrattamentiService.Nome(trattamento).ToLowerInvariant()}" : "")}.",
                origine: "Wubook",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "Wubook",
                cancellationToken: cancellationToken);

            // Una notifica per ordine, sulla prima camera: è quella con l'ospite, su cui si fonde
            // un'eventuale cancellazione appena arrivata (vedi AnnullaDaOtaAsync).
            if (camera.Indice == 0)
            {
                var camereTesto = camereNellOrdine > 1 ? $", {camereNellOrdine} camere" : "";
                await notificaService.RegistraNuovaOModificaWubookAsync(
                    strutturaId, entity.Id, nomeCanale,
                    booking.CustomerEmail, booking.CustomerName, booking.CustomerSurname,
                    titoloNuova: "Nuova prenotazione",
                    messaggioNuova: $"Nuova prenotazione da {nomeCanale}: {booking.CheckIn:dd/MM/yyyy}–{booking.CheckOut:dd/MM/yyyy}{camereTesto}.",
                    titoloModifica: "Prenotazione modificata",
                    messaggioModifica: $"Prenotazione da {nomeCanale} modificata: ora {booking.CheckIn:dd/MM/yyyy}–{booking.CheckOut:dd/MM/yyyy}{camereTesto}.",
                    cancellationToken);
            }
        }
        else
        {
            await prenotazioni.UpdateAsync(entity, cancellationToken);
            var serviziCambiati = await ImportaServiziSitoAsync(strutturaId, entity, booking, camera.Indice, nomeCanale, cancellationToken);

            // Rete di sicurezza per l'arrivo mai segnalato. I passi dell'import non sono in
            // transazione (prima la prenotazione, poi la notifica, poi l'ospite, ciascuno con il
            // proprio salvataggio): se si interrompe in mezzo, la prenotazione resta salvata senza
            // notifica e l'evento viene ritentato dal polling — ma al secondo giro non risulta più
            // "nuova" e finisce qui, dove prima non veniva segnalato nulla. Limitato agli arrivi
            // recenti: su una prenotazione di settimane fa un "Nuova prenotazione" sarebbe fuorviante
            // (e per le prenotazioni importate prima che il centro notifiche esistesse, sbagliato).
            var arrivoRecente = entity.CreatedAtUtc >= DateTime.UtcNow.AddHours(-OreRecuperoArrivoNonNotificato);
            if (arrivoRecente && camera.Indice == 0 && await notificaService.RecuperaArrivoNonNotificatoAsync(
                strutturaId, entity.Id, nomeCanale,
                "Nuova prenotazione",
                $"Nuova prenotazione da {nomeCanale}: {booking.CheckIn:dd/MM/yyyy}–{booking.CheckOut:dd/MM/yyyy}.",
                cancellationToken))
            {
                await logEventi.RegistraAsync(
                    LivelloLog.Warning,
                    $"Prenotazione #{entity.NumeroPrenotazione} da {nomeCanale} (rcode={booking.RCode}): arrivo segnalato in ritardo, la notifica non era stata creata al primo import (import interrotto e poi ripetuto).",
                    origine: "Wubook",
                    clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                    strutturaId: strutturaId,
                    categoria: "Wubook",
                    cancellationToken: cancellationToken);
                return EsitoBooking.Aggiornata;
            }

            // Solo le differenze che contano per chi gestisce la struttura: un rcode può ripassare
            // identico (ri-elaborazione dello stesso evento, re-import manuale) e in quel caso non
            // c'è nulla da segnalare, altrimenti il centro notifiche si riempirebbe di rumore.
            var cambiamenti = new List<string>();
            if (primaCheckIn != entity.CheckIn || primaCheckOut != entity.CheckOut)
            {
                cambiamenti.Add($"date da {primaCheckIn:dd/MM/yyyy}–{primaCheckOut:dd/MM/yyyy} a {entity.CheckIn:dd/MM/yyyy}–{entity.CheckOut:dd/MM/yyyy}");
            }

            if (primaImporto != entity.ImportoPrenotazione)
            {
                cambiamenti.Add($"importo da {primaImporto:N2} € a {entity.ImportoPrenotazione:N2} €");
            }

            if (primaNumeroOspiti != entity.NumeroOspiti)
            {
                cambiamenti.Add($"ospiti da {primaNumeroOspiti} a {entity.NumeroOspiti}");
            }

            if (primaTipologiaId != entity.TipologiaId)
            {
                cambiamenti.Add($"tipologia ora '{tipologia.TipologiaCamera}'");
            }

            if (primaTrattamento != entity.Trattamento)
            {
                cambiamenti.Add($"trattamento da {NomeTrattamento(primaTrattamento)} a {NomeTrattamento(entity.Trattamento)}");
            }

            if (primaCameraId != entity.CameraId)
            {
                cambiamenti.Add(entity.CameraId is null ? "camera assegnata rimossa (serve riassegnazione manuale)" : "camera assegnata cambiata");
            }

            if (serviziCambiati is not null)
            {
                cambiamenti.Add(serviziCambiati);
            }

            if (cambiamenti.Count > 0)
            {
                var riepilogo = string.Join(", ", cambiamenti);
                await logEventi.RegistraAsync(
                    LivelloLog.Info,
                    $"Prenotazione #{entity.NumeroPrenotazione}{dellOrdine} da {nomeCanale} (rcode={booking.RCode}) modificata dall'OTA: {riepilogo}.",
                    origine: "Wubook",
                    clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                    strutturaId: strutturaId,
                    categoria: "Wubook",
                    cancellationToken: cancellationToken);

                await notificaService.RegistraModificaWubookAsync(
                    strutturaId, entity.Id, nomeCanale,
                    "Prenotazione modificata",
                    $"Prenotazione #{entity.NumeroPrenotazione}{dellOrdine} ({nomeCanale}) modificata: {riepilogo}.",
                    cancellationToken);
            }
        }

        // L'OTA dà solo chi ha prenotato: è l'ospite della prima camera. Nelle altre stanno altre
        // persone (l'altra famiglia): scriverci il suo nome finirebbe su una schedina sbagliata.
        if (camera.Indice > 0)
        {
            return nuova ? EsitoBooking.Creata : EsitoBooking.Aggiornata;
        }

        var ospite = await ospiti.GetByPrenotazioneAsync(entity.Id, cancellationToken);
        if (ospite is null)
        {
            ospite = new Ospite { StrutturaId = strutturaId, PrenotazioneId = entity.Id };
            ospiti.Add(ospite);
        }

        ospite.Nome = booking.CustomerName;
        ospite.Cognome = booking.CustomerSurname;
        ospite.Email = booking.CustomerEmail;
        ospite.Cittadinanza = booking.CustomerCountry;
        ospite.LuogoResidenza = booking.CustomerCity;
        ospite.Permanenza = (booking.CheckOut.Date - booking.CheckIn.Date).Days;
        ospite.TipoOspite = camera.NumeroOspiti > 1 ? "CAPO FAMIGLIA" : "OSPITE SINGOLO";
        ospite.UpdatedAtUtc = DateTime.UtcNow;
        await ospiti.SaveChangesAsync(cancellationToken);

        return nuova ? EsitoBooking.Creata : EsitoBooking.Aggiornata;
    }

    /// <summary>
    /// Registra il canale risolto da Wubook (es. "Sito Web" per id_channel non mappato/0, o il nome
    /// del canale OTA) tra i canali/agenzie suggeriti della struttura, se non già presente — così il
    /// dropdown "Agenzia/canale" in UI lo mostra subito, replicando il comportamento del legacy dove
    /// la lista si autoalimentava dai valori effettivamente usati nelle prenotazioni.
    /// </summary>
    private async Task AssicuraCanaleVenditaAsync(Guid strutturaId, string nomeCanale, CancellationToken cancellationToken)
    {
        if (await canaliVendita.ExistsByDescrizioneAsync(strutturaId, nomeCanale, escludiId: null, cancellationToken))
        {
            return;
        }

        await canaliVendita.AddAsync(new SettingAgenzia { StrutturaId = strutturaId, Descrizione = nomeCanale }, cancellationToken);
    }

    /// <summary>
    /// Come la scelta a mano, i prezzi del trattamento si copiano dal listino; se la prenotazione arriva
    /// dal sito web con i prezzi a cui l'ha venduto (prezziVenduti), valgono quelli, così le modifiche
    /// successive ricalcolano con quanto l'ospite ha pagato davvero. A differenza della scelta a mano,
    /// un trattamento che la struttura non ha attivo si registra lo stesso (l'ospite l'ha già pagato e
    /// va servito), senza prezzi e con un avviso a log.
    /// </summary>
    private async Task ImpostaTrattamentoOtaAsync(
        Guid strutturaId, Prenotazione entity, TipoTrattamento? trattamento, PrezziTrattamento? prezziVenduti, string nomeCanale, int rcode, CancellationToken cancellationToken)
    {
        var listino = trattamento is { } tipo && prezziVenduti is null ? await listiniTrattamento.GetAsync(strutturaId, tipo, cancellationToken) : null;
        var prezzi = trattamento is null
            ? null
            : prezziVenduti ?? (listino is { Attivo: true } ? TrattamentiService.PrezziDa(listino) : null);
        entity.Trattamento = trattamento;
        entity.TrattamentoPrezzoAdulto = prezzi?.PrezzoAdulto;
        entity.TrattamentoPrezzoBambino = prezzi?.PrezzoBambino;
        entity.TrattamentoEtaMassimaBambini = prezzi?.EtaMassimaBambini;

        if (trattamento is { } venduto && prezzi is null)
        {
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                $"Prenotazione #{entity.NumeroPrenotazione} da {nomeCanale} (rcode={rcode}): l'OTA ha venduto \"{TrattamentiService.Nome(venduto)}\", che in Impostazioni → Servizi non è attivo. Registrato sulla prenotazione senza prezzi.",
                origine: "Wubook",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "Wubook",
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Servizi extra venduti dal sito web (vedi ServiziSitoWeb), sulla prima camera dell'ordine: il
    /// servizio si riconosce dal codice, il prezzo è quello pagato sul sito (come ogni vendita, copiato
    /// sulla riga). Il loro importo è già nel totale che manda WuBook, quindi il totale non cambia; la
    /// fattura li separa dal soggiorno. Tocca solo le righe che ha aggiunto lui: quelle aggiunte a mano
    /// restano. Un servizio a notte vale per una notte, così l'importo resta quantità × prezzo come sul
    /// sito. Ritorna la descrizione del cambiamento, null se non è cambiato nulla.
    /// </summary>
    private async Task<string?> ImportaServiziSitoAsync(
        Guid strutturaId, Prenotazione entity, WubookPrenotazione booking, int indiceCamera, string nomeCanale, CancellationToken cancellationToken)
    {
        if (indiceCamera > 0 || entity.CheckIn is not { } checkIn)
        {
            return null;
        }

        var venduti = ServiziSitoWeb.Leggi(booking.Extra);
        var esistenti = await serviziStruttura.ListByPrenotazioneAsync(strutturaId, entity.Id, cancellationToken);
        var dalSito = esistenti.Where(r => r.AggiuntoDa == ServiziSitoWeb.AggiuntoDa).ToList();
        if (venduti.Count == 0 && dalSito.Count == 0)
        {
            return null;
        }

        var listino = await serviziStruttura.ListByStrutturaAsync(strutturaId, cancellationToken);
        var arrivo = DateOnly.FromDateTime(checkIn);
        var daRiusare = dalSito.ToList();
        var nuove = new List<PrenotazioneServizio>();
        var sconosciuti = new List<string>();
        foreach (var venduto in venduti)
        {
            var servizio = listino.FirstOrDefault(s => s.Codice == venduto.Codice);
            if (servizio is null)
            {
                sconosciuti.Add(venduto.Codice);
                continue;
            }

            // Stessa riga (stesso Id) solo a parità di servizio e prezzo: il salvataggio di una riga
            // esistente aggiorna quantità e date, non il prezzo con cui è stata venduta.
            var riga = daRiusare.FirstOrDefault(r => r.ServizioId == servizio.Id && r.PrezzoUnitario == venduto.PrezzoUnitario);
            if (riga is not null)
            {
                daRiusare.Remove(riga);
            }

            nuove.Add(new PrenotazioneServizio
            {
                Id = riga?.Id ?? Guid.NewGuid(),
                CreatedAtUtc = riga?.CreatedAtUtc ?? DateTime.UtcNow,
                StrutturaId = strutturaId,
                PrenotazioneId = entity.Id,
                ServizioId = servizio.Id,
                Nome = riga?.Nome ?? servizio.Nome,
                Modalita = riga?.Modalita ?? servizio.Modalita,
                PrezzoUnitario = venduto.PrezzoUnitario,
                Quantita = Math.Min(venduto.Quantita, ServiziService.QuantitaMassima),
                Dal = arrivo,
                Al = ServiziService.PerNotte(riga?.Modalita ?? servizio.Modalita) ? arrivo.AddDays(1) : null,
                Origine = OrigineServizio.ConLaPrenotazione,
                AggiuntoDa = ServiziSitoWeb.AggiuntoDa,
            });
        }

        if (sconosciuti.Count > 0)
        {
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                $"Prenotazione #{entity.NumeroPrenotazione} da {nomeCanale} (rcode={booking.RCode}): il sito ha venduto servizi con codice {string.Join(", ", sconosciuti)}, che nessun servizio in Impostazioni → Servizi ha. Non aggiunti alla prenotazione: il loro importo è comunque nel totale, va separato a mano.",
                origine: "Wubook",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "Wubook",
                cancellationToken: cancellationToken);
        }

        static string Chiave(PrenotazioneServizio r) => $"{r.ServizioId}|{r.Quantita}|{r.PrezzoUnitario}";
        if (dalSito.Select(Chiave).Order().SequenceEqual(nuove.Select(Chiave).Order()))
        {
            return null;
        }

        var righe = esistenti.Where(r => r.AggiuntoDa != ServiziSitoWeb.AggiuntoDa).Concat(nuove).ToList();
        await serviziStruttura.SostituisciDellaPrenotazioneAsync(strutturaId, entity.Id, righe, cancellationToken);
        return $"servizi dal sito ora {ServiziService.Descrivi(nuove)}";
    }

    private static string NomeTrattamento(TipoTrattamento? tipo) => tipo is { } t ? TrattamentiService.Nome(t).ToLowerInvariant() : "solo pernottamento";
}
