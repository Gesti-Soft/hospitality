using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Notifiche;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
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
    NotificaService notificaService)
{
    private enum EsitoBooking { Creata, Aggiornata, Annullata, Ignorata }

    /// <summary>Entro quante ore dalla creazione ha ancora senso segnalare come "arrivata" una prenotazione la cui notifica non era mai stata creata (vedi il recupero in ImportaBookingAsync).</summary>
    private const int OreRecuperoArrivoNonNotificato = 24;

    public async Task<RisultatoSincronizzazionePrenotazioni> SincronizzaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);
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
                    $"Import prenotazione Wubook rcode={booking.RCode}: {ex.Message}",
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

    private async Task<EsitoBooking> ImportaBookingAsync(Guid strutturaId, WubookPrenotazione booking, string nomeCanale, CancellationToken cancellationToken)
    {
        if (!int.TryParse(booking.CameraIdWubookRaw, out var idCameraWubook))
        {
            throw new InvalidOperationException($"Id camera Wubook non numerico: '{booking.CameraIdWubookRaw}'.");
        }

        var tipologia = await tipologie.GetByIdWubookAsync(strutturaId, idCameraWubook, cancellationToken)
            ?? throw new InvalidOperationException($"Nessuna tipologia locale associata a IdCameraWubook={idCameraWubook}.");

        var esistente = await prenotazioni.GetByIdPrenotazioneWubookAsync(strutturaId, booking.RCode, cancellationToken);

        // Status 5 = prenotazione cancellata lato Wubook (vedi report Fase 5 sez. C).
        if (booking.Status == 5)
        {
            if (esistente is null)
            {
                return EsitoBooking.Ignorata;
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
                    $"Wubook segnala come cancellata la prenotazione #{numeroVisualizzatoInCorso} (rcode={booking.RCode}), ma risulta già In corso (check-in effettuato): nessuna modifica automatica, verificare manualmente.",
                    origine: "Wubook",
                    clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                    strutturaId: strutturaId,
                    categoria: "Wubook",
                    cancellationToken: cancellationToken);
                await notificaService.CreaPerPrenotazioneSeNonEsisteAsync(
                    strutturaId, TipoNotifica.PrenotazioneAnnullata, esistente.Id,
                    "Cancellazione da verificare",
                    $"Wubook segnala come cancellata la prenotazione #{numeroVisualizzatoInCorso}, ma l'ospite ha già fatto check-in: verificare manualmente.",
                    cancellationToken);
                return EsitoBooking.Ignorata;
            }

            // Arrivati qui la prenotazione non era In corso (vedi controllo sopra), quindi il
            // check-in non è mai avvenuto e la camera non è mai stata toccata da questa prenotazione
            // — non c'è nulla da liberare. Se la camera risulta occupata/non pronta, è per un motivo
            // indipendente (altro soggiorno in corso, blocco manuale) e non va alterato qui.
            var numeroVisualizzato = esistente.NumeroPrenotazione ?? esistente.Id.ToString()[..8];
            var canaleCancellata = esistente.Agenzia ?? nomeCanale;

            esistente.StatoPrenotazione = StatoPrenotazione.Annullata;
            esistente.ImportoPrenotazione = 0;
            esistente.ImportoPagato = 0;
            esistente.ImportoTotale = 0;
            esistente.UpdatedAtUtc = DateTime.UtcNow;
            await prenotazioni.UpdateAsync(esistente, cancellationToken);

            await logEventi.RegistraAsync(
                LivelloLog.Info,
                $"Prenotazione #{numeroVisualizzato} annullata da Wubook (rcode={booking.RCode}).",
                origine: "Wubook",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "Wubook",
                cancellationToken: cancellationToken);

            // Non ancora una notifica visibile: Wubook, quando un operatore modifica una prenotazione
            // da un canale OTA, manda la cancellazione del vecchio rcode e subito dopo una nuova
            // prenotazione con i dati aggiornati — resta InAttesa per una breve finestra di grazia, in
            // modo da poterla fondere con quella nuova (vedi RegistraNuovaOModificaWubookAsync) invece
            // di notificare due volte la stessa modifica.
            await notificaService.RegistraCancellazioneWubookAsync(
                strutturaId, esistente.Id, canaleCancellata,
                "Prenotazione cancellata",
                $"Prenotazione #{numeroVisualizzato} ({canaleCancellata}) cancellata da Wubook.",
                cancellationToken);

            return EsitoBooking.Annullata;
        }

        var nuova = esistente is null;
        var entity = esistente ?? new Prenotazione
        {
            StrutturaId = strutturaId,
            IdPrenotazioneWubook = booking.RCode,
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
                    $"Prenotazione #{numeroVisualizzatoSenzaCamera} da {nomeCanale} (rcode={booking.RCode}): nessuna camera libera nel pool '{tipologia.TipologiaCamera}' per {booking.CheckIn:dd/MM/yyyy}–{booking.CheckOut:dd/MM/yyyy} — registrata senza camera assegnata, serve assegnazione manuale.",
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
        entity.ImportoPrenotazione = booking.Importo;
        entity.ImportoTotale = booking.Importo;
        entity.CheckIn = booking.CheckIn;
        entity.CheckOut = booking.CheckOut;
        entity.NumeroOspiti = booking.Adulti + booking.Bambini;
        entity.Anno = booking.CheckIn.Year;
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

            // L'arrivo di una prenotazione da OTA va sempre a log, non solo quando qualcosa va
            // storto: prima era l'unico evento Wubook a non lasciare traccia (cancellazione e
            // fallimenti sì, l'import riuscito no), quindi chi non aveva visto passare la notifica
            // non aveva più nessun modo di sapere quando e da dove fosse arrivata.
            await logEventi.RegistraAsync(
                LivelloLog.Info,
                $"Nuova prenotazione #{entity.NumeroPrenotazione} da {nomeCanale} (rcode={booking.RCode}): {booking.CheckIn:dd/MM/yyyy}–{booking.CheckOut:dd/MM/yyyy}, {entity.NumeroOspiti} ospiti, {booking.Importo:N2} €.",
                origine: "Wubook",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "Wubook",
                cancellationToken: cancellationToken);

            await notificaService.RegistraNuovaOModificaWubookAsync(
                strutturaId, entity.Id, nomeCanale,
                booking.CustomerEmail, booking.CustomerName, booking.CustomerSurname,
                titoloNuova: "Nuova prenotazione",
                messaggioNuova: $"Nuova prenotazione da {nomeCanale}: {booking.CheckIn:dd/MM/yyyy}–{booking.CheckOut:dd/MM/yyyy}.",
                titoloModifica: "Prenotazione modificata",
                messaggioModifica: $"Prenotazione da {nomeCanale} modificata: ora {booking.CheckIn:dd/MM/yyyy}–{booking.CheckOut:dd/MM/yyyy}.",
                cancellationToken);
        }
        else
        {
            await prenotazioni.UpdateAsync(entity, cancellationToken);

            // Rete di sicurezza per l'arrivo mai segnalato. I passi dell'import non sono in
            // transazione (prima la prenotazione, poi la notifica, poi l'ospite, ciascuno con il
            // proprio salvataggio): se si interrompe in mezzo, la prenotazione resta salvata senza
            // notifica e l'evento viene ritentato dal polling — ma al secondo giro non risulta più
            // "nuova" e finisce qui, dove prima non veniva segnalato nulla. Limitato agli arrivi
            // recenti: su una prenotazione di settimane fa un "Nuova prenotazione" sarebbe fuorviante
            // (e per le prenotazioni importate prima che il centro notifiche esistesse, sbagliato).
            var arrivoRecente = entity.CreatedAtUtc >= DateTime.UtcNow.AddHours(-OreRecuperoArrivoNonNotificato);
            if (arrivoRecente && await notificaService.RecuperaArrivoNonNotificatoAsync(
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

            if (primaCameraId != entity.CameraId)
            {
                cambiamenti.Add(entity.CameraId is null ? "camera assegnata rimossa (serve riassegnazione manuale)" : "camera assegnata cambiata");
            }

            if (cambiamenti.Count > 0)
            {
                var riepilogo = string.Join(", ", cambiamenti);
                await logEventi.RegistraAsync(
                    LivelloLog.Info,
                    $"Prenotazione #{entity.NumeroPrenotazione} da {nomeCanale} (rcode={booking.RCode}) modificata da Wubook: {riepilogo}.",
                    origine: "Wubook",
                    clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                    strutturaId: strutturaId,
                    categoria: "Wubook",
                    cancellationToken: cancellationToken);

                await notificaService.RegistraModificaWubookAsync(
                    strutturaId, entity.Id, nomeCanale,
                    "Prenotazione modificata",
                    $"Prenotazione #{entity.NumeroPrenotazione} ({nomeCanale}) modificata: {riepilogo}.",
                    cancellationToken);
            }
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
        ospite.TipoOspite = booking.Adulti + booking.Bambini > 1 ? "CAPO FAMIGLIA" : "OSPITE SINGOLO";
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
}
