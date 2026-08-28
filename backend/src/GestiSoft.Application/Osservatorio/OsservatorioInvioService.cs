using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Osservatorio;

public record RisultatoInvioOsservatorio(int ArriviInviati, int CheckoutInviati, int GiorniChiusi, string? Messaggio);

public record SchedinaOsservatorio(Guid OspiteId, Guid? PrenotazioneId, string NomeOspite, string? Camera, DateTime? CheckIn, DateTime? CheckOut, bool ArrivoInviato, bool? PartenzaInviata);

/// <summary>
/// Invio giornaliero all'Osservatorio Turistico — porta SendSchedinaOseervatorio/CloseDay di
/// StatePoliceLogic del legacy: login, recupero di eventuali giorni arretrati non chiusi (solo
/// checkout + enddayfrompms, niente nuovi arrivi per giorni passati), poi per il giorno corrente
/// invio degli arrivi e chiusura giornata, logout.
/// A differenza del legacy: (1) il cursore di chiusura è per Appartamento invece che globale
/// all'installazione — vedi <see cref="OsservatorioAppartamento"/>; (2) se enddayfrompms fallisce
/// il cursore NON avanza (il legacy lo avanzava comunque, desincronizzandosi dal server — bug
/// trovato leggendo il codice, non solo testando dal vivo); (3) gli arrivi/checkout di un intero
/// appartamento in un giorno vengono raggruppati in una sola chiamata invece che una per ospite
/// (batching esplicitamente autorizzato dall'utente per questa fase, non verificato contro un
/// endpoint reale).
/// </summary>
public class OsservatorioInvioService(
    IOspiteRepository ospiti,
    IPrenotazioneRepository prenotazioni,
    IOsservatorioAppartamentoRepository appartamenti,
    IOsservatorioInvioRepository invii,
    IAnagraficaAlloggiatiWebRepository anagrafica,
    IOsservatorioClient client,
    PermessoStrutturaGuard permessoGuard,
    ConcessioneServiziGuard concessioneGuard)
{
    public async Task<RisultatoInvioOsservatorio> InviaOraAsync(ICurrentUser currentUser, Guid strutturaId, Guid appartamentoId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceWrite, cancellationToken);

        var appartamento = await appartamenti.GetAsync(strutturaId, appartamentoId, cancellationToken)
            ?? throw new NotFoundException("Appartamento Osservatorio Turistico non trovato.");

        return await ProcessaAppartamentoAsync(strutturaId, appartamento, cancellationToken);
    }

    /// <summary>Usato dal job Quartz schedulato (Worker) — nessun ICurrentUser, un appartamento alla volta: un fallimento su uno non blocca gli altri.</summary>
    public async Task<IReadOnlyList<RisultatoInvioOsservatorio>> InviaSistemaAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var lista = await appartamenti.ListByStrutturaAsync(strutturaId, cancellationToken);
        var risultati = new List<RisultatoInvioOsservatorio>();

        foreach (var appartamento in lista)
        {
            try
            {
                risultati.Add(await ProcessaAppartamentoAsync(strutturaId, appartamento, cancellationToken));
            }
            catch (Exception ex)
            {
                await SalvaErroreAsync(appartamento, ex.Message, cancellationToken);
                risultati.Add(new RisultatoInvioOsservatorio(0, 0, 0, ex.Message));
            }
        }

        return risultati;
    }

    /// <summary>
    /// Elenco arrivi/partenze recenti (30 giorni) di un appartamento per la schermata operativa —
    /// da inviare e già inviati. La partenza si considera inviata se il cursore di chiusura
    /// giornata dell'appartamento ha già superato la data di check-out (nessun flag dedicato per
    /// singola prenotazione: il checkout viene chiuso per giorno, non per ospite).
    /// </summary>
    public async Task<IReadOnlyList<SchedinaOsservatorio>> ListSchedineAsync(ICurrentUser currentUser, Guid strutturaId, Guid appartamentoId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var appartamento = await appartamenti.GetAsync(strutturaId, appartamentoId, cancellationToken)
            ?? throw new NotFoundException("Appartamento Osservatorio Turistico non trovato.");

        var tipologieIds = appartamento.Tipologie.Select(t => t.TipologiaId).ToHashSet();
        var recenti = await ospiti.ListRecentiOsservatorioAsync(strutturaId, tipologieIds, DateTime.UtcNow.Date.AddDays(-30), cancellationToken);

        return recenti.Select(o => new SchedinaOsservatorio(
            o.Id,
            o.PrenotazioneId,
            $"{o.Cognome} {o.Nome}".Trim(),
            o.Prenotazione?.Camera?.Nome,
            o.Prenotazione?.CheckIn,
            o.Prenotazione?.CheckOut,
            o.Prenotazione?.PMS ?? false,
            o.Prenotazione?.CheckOut is { } checkOut ? appartamento.CursoreDataAtUtc?.Date > checkOut.Date : null)).ToList();
    }

    private async Task<RisultatoInvioOsservatorio> ProcessaAppartamentoAsync(Guid strutturaId, OsservatorioAppartamento appartamento, CancellationToken cancellationToken)
    {
        await concessioneGuard.EnsureOsservatorioAsync(strutturaId, cancellationToken);

        if (string.IsNullOrWhiteSpace(appartamento.EntityCode) || string.IsNullOrWhiteSpace(appartamento.Password) || string.IsNullOrWhiteSpace(appartamento.HotelCode))
        {
            await SalvaErroreAsync(appartamento, "Credenziali Osservatorio Turistico non configurate.", cancellationToken);
            return new RisultatoInvioOsservatorio(0, 0, 0, "Credenziali non configurate.");
        }

        if (appartamento.Tipologie.Count == 0)
        {
            await SalvaErroreAsync(appartamento, "Nessuna tipologia camera associata a questo appartamento.", cancellationToken);
            return new RisultatoInvioOsservatorio(0, 0, 0, "Nessuna tipologia camera associata.");
        }

        var oggi = DateTime.UtcNow.Date;

        // Giornata di oggi già chiusa con successo (cursore già avanzato a domani): niente da fare,
        // evita un login/logout inutile verso il servizio esterno ad ogni giro del job (ogni minuto).
        if (appartamento.CursoreDataAtUtc?.Date > oggi)
        {
            return new RisultatoInvioOsservatorio(0, 0, 0, null);
        }

        var login = await client.LoginAsync(appartamento.EntityCode, appartamento.Password, cancellationToken);
        if (!login.Ok || login.Token is null)
        {
            await SalvaErroreAsync(appartamento, login.Errore ?? "Login non riuscito.", cancellationToken);
            return new RisultatoInvioOsservatorio(0, 0, 0, login.Errore);
        }

        var tipologieIds = appartamento.Tipologie.Select(t => t.TipologiaId).ToHashSet();
        var cursore = appartamento.CursoreDataAtUtc?.Date ?? oggi;

        int arriviInviati = 0, checkoutInviati = 0, giorniChiusi = 0;

        try
        {
            // Recupero arretrati: solo checkout + chiusura giornata, niente nuovi arrivi per giorni
            // passati (fedele al legacy — gli arrivi si inviano solo per il giorno corrente).
            while (cursore < oggi)
            {
                checkoutInviati += await ChiudiGiornataAsync(strutturaId, appartamento, login.Token, cursore, tipologieIds, cancellationToken);
                giorniChiusi++;
                cursore = cursore.AddDays(1);
                appartamento.CursoreDataAtUtc = cursore;
                await appartamenti.UpdateAsync(appartamento, cancellationToken);
            }

            if (cursore == oggi)
            {
                arriviInviati = await InviaArriviAsync(strutturaId, appartamento, login.Token, oggi, tipologieIds, cancellationToken);
                checkoutInviati += await ChiudiGiornataAsync(strutturaId, appartamento, login.Token, oggi, tipologieIds, cancellationToken);
                giorniChiusi++;
                appartamento.CursoreDataAtUtc = oggi.AddDays(1);
            }

            appartamento.UltimoInvioAtUtc = DateTime.UtcNow;
            appartamento.UltimeSchedineInviate = arriviInviati;
            appartamento.UltimoErrore = null;
            await appartamenti.UpdateAsync(appartamento, cancellationToken);

            return new RisultatoInvioOsservatorio(arriviInviati, checkoutInviati, giorniChiusi, null);
        }
        catch (Exception ex)
        {
            // Il cursore persistito resta fermo all'ultimo giorno chiuso con successo (vedi sopra,
            // salvato incrementalmente ad ogni giorno di arretrato e solo a fine giornata corrente):
            // un fallimento su un giorno non lo fa mai avanzare oltre, cosa che il legacy invece
            // faceva (vedi doc della classe) — il prossimo giro ritenta lo stesso giorno.
            await SalvaErroreAsync(appartamento, ex.Message, cancellationToken);
            return new RisultatoInvioOsservatorio(arriviInviati, checkoutInviati, giorniChiusi, ex.Message);
        }
        finally
        {
            await client.LogoutAsync(login.Token, cancellationToken);
        }
    }

    private async Task<int> InviaArriviAsync(Guid strutturaId, OsservatorioAppartamento appartamento, string token, DateTime giorno, IReadOnlyCollection<Guid> tipologieIds, CancellationToken cancellationToken)
    {
        var arrivi = await ospiti.ListArriviOsservatorioAsync(strutturaId, tipologieIds, giorno, cancellationToken);
        if (arrivi.Count == 0)
        {
            return 0;
        }

        var builder = await CreaBuilderAsync(cancellationToken);
        var righeDaSalvare = new List<OsservatorioInvio>();
        var stays = new List<OsservatorioStayDto>();

        foreach (var ospite in arrivi)
        {
            var prenotazioneId = ospite.PrenotazioneId ?? Guid.Empty;
            var stayId = $"{giorno.Year}_{appartamento.ProssimoStayIdProgressivo:D5}";
            appartamento.ProssimoStayIdProgressivo++;

            var guestIdCapofamiglia = StayBuilderOsservatorio.GeneraGuestId(ospite.Cognome, ospite.Nome, ospite.DataNascita, prenotazioneId);
            var guestIdMembri = ospite.Membri.ToDictionary(
                m => m.Id,
                m => StayBuilderOsservatorio.GeneraGuestId(m.Cognome, m.Nome, m.DataNascita, prenotazioneId));

            stays.Add(builder.CostruisciArrivo(ospite, stayId, guestIdCapofamiglia, guestIdMembri));

            righeDaSalvare.Add(new OsservatorioInvio { StrutturaId = strutturaId, PrenotazioneId = prenotazioneId, OspiteRigaId = null, StayId = stayId, GuestId = guestIdCapofamiglia, DataInvioUtc = DateTime.UtcNow });
            foreach (var (membroId, guestId) in guestIdMembri)
            {
                righeDaSalvare.Add(new OsservatorioInvio { StrutturaId = strutturaId, PrenotazioneId = prenotazioneId, OspiteRigaId = membroId, StayId = stayId, GuestId = guestId, DataInvioUtc = DateTime.UtcNow });
            }
        }

        var esito = await client.SendArrivalsAsync(token, appartamento.HotelCode!, stays, cancellationToken);
        if (!esito.Ok)
        {
            throw new InvalidOperationException(esito.Errore ?? "Invio arrivi rifiutato dall'Osservatorio Turistico.");
        }

        await invii.AddRangeAsync(righeDaSalvare, cancellationToken);

        foreach (var ospite in arrivi)
        {
            if (ospite.PrenotazioneId is not { } prenotazioneId)
            {
                continue;
            }

            var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken);
            if (prenotazione is null)
            {
                continue;
            }

            prenotazione.PMS = true;
            prenotazione.UpdatedAtUtc = DateTime.UtcNow;
            await prenotazioni.UpdateAsync(prenotazione, cancellationToken);
        }

        return arrivi.Count;
    }

    private async Task<int> ChiudiGiornataAsync(Guid strutturaId, OsservatorioAppartamento appartamento, string token, DateTime giorno, IReadOnlyCollection<Guid> tipologieIds, CancellationToken cancellationToken)
    {
        var checkout = await ospiti.ListCheckoutOsservatorioAsync(strutturaId, tipologieIds, giorno, cancellationToken);
        var inviati = 0;

        if (checkout.Count > 0)
        {
            var builder = await CreaBuilderAsync(cancellationToken);
            var stays = new List<OsservatorioStayDto>();

            foreach (var ospite in checkout)
            {
                if (ospite.PrenotazioneId is not { } prenotazioneId)
                {
                    continue;
                }

                var storico = await invii.ListByPrenotazioneAsync(prenotazioneId, cancellationToken);
                var invioCapofamiglia = storico.FirstOrDefault(s => s.OspiteRigaId == null);
                if (invioCapofamiglia is null)
                {
                    // Nessun arrivo registrato per questa prenotazione presso l'Osservatorio: non
                    // si può generare un checkout coerente (GuestId/StayId mancanti). Il legacy in
                    // questo caso avrebbe comunque inviato un GuestId nullo — validazione aggiunta
                    // qui, assente nel legacy.
                    continue;
                }

                var guestIdMembri = ospite.Membri
                    .Select(m => (m.Id, GuestId: storico.FirstOrDefault(s => s.OspiteRigaId == m.Id)?.GuestId))
                    .Where(x => x.GuestId is not null)
                    .ToDictionary(x => x.Id, x => x.GuestId!);

                stays.Add(builder.CostruisciCheckout(ospite, invioCapofamiglia.StayId, invioCapofamiglia.GuestId, guestIdMembri));
            }

            if (stays.Count > 0)
            {
                var esito = await client.SendCheckoutsAsync(token, appartamento.HotelCode!, stays, cancellationToken);
                if (!esito.Ok)
                {
                    throw new InvalidOperationException(esito.Errore ?? "Invio checkout rifiutato dall'Osservatorio Turistico.");
                }

                inviati = stays.Count;
            }
        }

        var chiusura = await client.EndDayAsync(token, appartamento.HotelCode!, giorno, cancellationToken);
        if (!chiusura.Ok)
        {
            throw new InvalidOperationException(chiusura.Errore ?? $"Chiusura giornata {giorno:dd/MM/yyyy} rifiutata dall'Osservatorio Turistico.");
        }

        return inviati;
    }

    private async Task<StayBuilderOsservatorio> CreaBuilderAsync(CancellationToken cancellationToken) => new(
        await anagrafica.ListLuoghiAsync(cancellationToken),
        await anagrafica.ListTipiAlloggiatoAsync(cancellationToken));

    private async Task SalvaErroreAsync(OsservatorioAppartamento appartamento, string errore, CancellationToken cancellationToken)
    {
        appartamento.UltimoErrore = errore;
        appartamento.UltimoInvioAtUtc = DateTime.UtcNow;
        await appartamenti.UpdateAsync(appartamento, cancellationToken);
    }
}
