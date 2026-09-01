using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Logging;
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
    ICameraRepository camere,
    ICanaleVenditaRepository canaliVendita,
    IWubookClient wubookClient,
    WubookLicenzaService licenzaService,
    IStrutturaRepository strutture,
    PermessoStrutturaGuard permessoGuard,
    ILogEventoService logEventi)
{
    private enum EsitoBooking { Creata, Aggiornata, Annullata, Ignorata }

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

        var camera = await camere.GetByIdWubookAsync(strutturaId, idCameraWubook, cancellationToken)
            ?? throw new InvalidOperationException($"Nessuna camera locale associata a IdCameraWubook={idCameraWubook}.");

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
                await logEventi.RegistraAsync(
                    LivelloLog.Warning,
                    $"Wubook segnala come cancellata la prenotazione #{esistente.NumeroPrenotazione ?? esistente.Id.ToString()[..8]} (rcode={booking.RCode}), ma risulta già In corso (check-in effettuato): nessuna modifica automatica, verificare manualmente.",
                    origine: "Wubook",
                    clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                    strutturaId: strutturaId,
                    categoria: "Wubook",
                    cancellationToken: cancellationToken);
                return EsitoBooking.Ignorata;
            }

            // Arrivati qui la prenotazione non era In corso (vedi controllo sopra), quindi il
            // check-in non è mai avvenuto e la camera non è mai stata toccata da questa prenotazione
            // — non c'è nulla da liberare. Se la camera risulta occupata/non pronta, è per un motivo
            // indipendente (altro soggiorno in corso, blocco manuale) e non va alterato qui.
            esistente.StatoPrenotazione = StatoPrenotazione.Annullata;
            esistente.ImportoPrenotazione = 0;
            esistente.ImportoPagato = 0;
            esistente.ImportoTotale = 0;
            esistente.UpdatedAtUtc = DateTime.UtcNow;
            await prenotazioni.UpdateAsync(esistente, cancellationToken);

            await logEventi.RegistraAsync(
                LivelloLog.Info,
                $"Prenotazione #{esistente.NumeroPrenotazione ?? esistente.Id.ToString()[..8]} annullata da Wubook (rcode={booking.RCode}).",
                origine: "Wubook",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "Wubook",
                cancellationToken: cancellationToken);

            return EsitoBooking.Annullata;
        }

        var nuova = esistente is null;
        var entity = esistente ?? new Prenotazione
        {
            StrutturaId = strutturaId,
            IdPrenotazioneWubook = booking.RCode,
            StatoPrenotazione = StatoPrenotazione.Incompleta,
        };

        entity.CameraId = camera.Id;
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
            await prenotazioni.AddAsync(entity, cancellationToken);
        }
        else
        {
            await prenotazioni.UpdateAsync(entity, cancellationToken);
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
