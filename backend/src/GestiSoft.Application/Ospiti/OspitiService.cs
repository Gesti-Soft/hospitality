using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Ospiti;

public record MembroOspiteRequest(
    Guid? Id,
    Guid? CameraId,
    int? Permanenza,
    DateTime? DataNascita,
    Sesso? Sesso,
    string? Cognome,
    string? Nome,
    string? Cittadinanza,
    string? LuogoNascita,
    string? StatoNascita,
    string? LuogoResidenza,
    bool? PostoLetto,
    bool EsenteDaTassa);

public record SalvaSchedaOspitiRequest(
    string? TipoOspite,
    int? Permanenza,
    DateTime? DataNascita,
    Sesso? Sesso,
    string? Cognome,
    string? Nome,
    string? Cittadinanza,
    string? LuogoNascita,
    string? StatoNascita,
    string? LuogoResidenza,
    string? Email,
    string? Documento,
    string? NumeroDocumento,
    string? RilascioDocumento,
    bool EsenteDaTassa,
    IReadOnlyList<MembroOspiteRequest> Membri);

/// <summary>
/// Scheda ospiti/alloggiati collegata a una Prenotazione — porta OspitiLogic.AddOrUpdateOspiti
/// del legacy (vedi report Fase 3 sez. C.3) e il calcolo tassa di soggiorno (sez. C.7: esenzione
/// per residenza, per età via soglie configurate in Impostazioni, o manuale per persona).
/// </summary>
public class OspitiService(
    IOspiteRepository ospiti,
    IPrenotazioneRepository prenotazioni,
    IImpostazioniStrutturaRepository impostazioniStruttura,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<Ospite?> GetSchedaAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        await GetPrenotazioneOwnedAsync(strutturaId, prenotazioneId, cancellationToken);

        return await ospiti.GetByPrenotazioneAsync(prenotazioneId, cancellationToken);
    }

    public async Task<Ospite> SalvaSchedaAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        Guid prenotazioneId,
        SalvaSchedaOspitiRequest request,
        CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);

        var prenotazione = await GetPrenotazioneOwnedAsync(strutturaId, prenotazioneId, cancellationToken);

        var ospite = await ospiti.GetByPrenotazioneAsync(prenotazioneId, cancellationToken);
        if (ospite is null)
        {
            ospite = new Ospite { StrutturaId = strutturaId, PrenotazioneId = prenotazioneId };
            ospiti.Add(ospite);
        }

        ospite.TipoOspite = request.TipoOspite;
        ospite.Permanenza = request.Permanenza;
        ospite.DataNascita = request.DataNascita;
        ospite.Sesso = request.Sesso;
        ospite.Cognome = request.Cognome;
        ospite.Nome = request.Nome;
        ospite.Cittadinanza = request.Cittadinanza;
        ospite.LuogoNascita = request.LuogoNascita;
        ospite.StatoNascita = request.StatoNascita;
        ospite.LuogoResidenza = request.LuogoResidenza;
        ospite.Email = request.Email;
        ospite.Documento = request.Documento;
        ospite.NumeroDocumento = request.NumeroDocumento;
        ospite.RilascioDocumento = request.RilascioDocumento;
        ospite.EsenteDaTassa = request.EsenteDaTassa;
        ospite.UpdatedAtUtc = DateTime.UtcNow;

        SincronizzaMembri(ospite, strutturaId, request.Membri);

        prenotazione.NumeroOspiti = 1 + request.Membri.Count;
        // Se il toggle è disattivato, l'importo resta 0 anche aggiungendo ospiti — non va
        // "resuscitato" da un salvataggio della scheda che non c'entra col toggle stesso.
        prenotazione.TotalTax = prenotazione.TassaSoggiornoAttiva
            ? await CalcolaTassaSoggiornoAsync(strutturaId, prenotazione, ospite, cancellationToken)
            : 0;
        prenotazione.UpdatedAtUtc = DateTime.UtcNow;

        await ospiti.SaveChangesAsync(cancellationToken);
        return ospite;
    }

    private void SincronizzaMembri(Ospite ospite, Guid strutturaId, IReadOnlyList<MembroOspiteRequest> richiesti)
    {
        var idRichiesti = richiesti.Where(m => m.Id is not null).Select(m => m.Id!.Value).ToHashSet();

        foreach (var daRimuovere in ospite.Membri.Where(m => !idRichiesti.Contains(m.Id)).ToList())
        {
            ospite.Membri.Remove(daRimuovere);
            ospiti.RemoveMembro(daRimuovere);
        }

        foreach (var richiesta in richiesti)
        {
            var riga = richiesta.Id is { } id ? ospite.Membri.FirstOrDefault(m => m.Id == id) : null;
            if (riga is null)
            {
                riga = new OspiteRiga { StrutturaId = strutturaId };
                ospiti.AddMembro(riga);
                ospite.Membri.Add(riga);
            }

            riga.CameraId = richiesta.CameraId;
            riga.Permanenza = richiesta.Permanenza;
            riga.DataNascita = richiesta.DataNascita;
            riga.Sesso = richiesta.Sesso;
            riga.Cognome = richiesta.Cognome;
            riga.Nome = richiesta.Nome;
            riga.Cittadinanza = richiesta.Cittadinanza;
            riga.LuogoNascita = richiesta.LuogoNascita;
            riga.StatoNascita = richiesta.StatoNascita;
            riga.LuogoResidenza = richiesta.LuogoResidenza;
            riga.PostoLetto = richiesta.PostoLetto;
            riga.EsenteDaTassa = richiesta.EsenteDaTassa;
            riga.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Tassa di soggiorno: prezzo per notte (fino al tetto massimo di notti configurato) per ogni
    /// persona, ridotto della percentuale applicabile (esenzione manuale, residenza, fascia d'età —
    /// non necessariamente il 100%: un Comune può configurare uno sconto parziale, vedi
    /// <see cref="PercentualeRiduzione"/>). PayTourist (GET api/v1/reductions) non espone età/
    /// percentuale come dato strutturato in modo standard tra Comuni (solo testo libero nel nome —
    /// es. "Esenzione Minore Anni 12", "Anziani Ultrasettantacinquenni" — percentuale sì, quella è
    /// un campo vero), quindi soglie età e percentuali vengono proposte in automatico se PayTourist è
    /// attivo (euristica sul testo, vedi PayTouristConfigService.SuggerisciEtaEsenzioneTassaAsync) o
    /// impostate a mano altrimenti (vedi <see cref="ImpostazioniStruttura.TassaSoggiornoEtaEsenzioneMinori"/>).
    /// Pubblico perché riusato anche da <see cref="Prenotazioni.PrenotazioniService"/> per
    /// ricalcolare l'importo dopo un check-out anticipato (le notti effettive sono minori di
    /// quelle pianificate al momento del salvataggio della scheda ospiti).
    /// </summary>
    public async Task<decimal> CalcolaTassaSoggiornoAsync(Guid strutturaId, Prenotazione prenotazione, Ospite ospite, CancellationToken cancellationToken)
    {
        var impostazioni = await impostazioniStruttura.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (impostazioni is not { TassaSoggiornoPrezzo: > 0 } || prenotazione.CheckIn is null || prenotazione.CheckOut is null)
        {
            return 0;
        }

        var notti = (prenotazione.CheckOut.Value.Date - prenotazione.CheckIn.Value.Date).Days;
        var giorniMax = impostazioni.TassaSoggiornoMaxGiorni ?? int.MaxValue;
        var giorniEffettivi = Math.Min(Math.Max(notti, 0), giorniMax);
        if (giorniEffettivi <= 0)
        {
            return 0;
        }

        var comuneStruttura = impostazioni.ComuneAttivita;
        var importoPerPersona = impostazioni.TassaSoggiornoPrezzo.Value * giorniEffettivi;

        decimal totale = 0;
        foreach (var (esente, luogoResidenza, dataNascita) in PersoneScheda(ospite))
        {
            var percentualeRiduzione = PercentualeRiduzione(esente, luogoResidenza, dataNascita, comuneStruttura, prenotazione.CheckIn.Value, impostazioni);
            totale += importoPerPersona * (100m - percentualeRiduzione) / 100m;
        }

        return totale;
    }

    private static IEnumerable<(bool EsenteDaTassa, string? LuogoResidenza, DateTime? DataNascita)> PersoneScheda(Ospite ospite)
    {
        yield return (ospite.EsenteDaTassa, ospite.LuogoResidenza, ospite.DataNascita);
        foreach (var membro in ospite.Membri)
        {
            yield return (membro.EsenteDaTassa, membro.LuogoResidenza, membro.DataNascita);
        }
    }

    /// <summary>
    /// Percentuale di riduzione (0-100) applicabile a una persona — non binario "esente sì/no": un
    /// Comune può configurare uno sconto parziale (es. 50%) invece dell'esenzione piena su residenza
    /// o età (vedi <see cref="ImpostazioniStruttura.TassaSoggiornoPercentualeResidenti"/>). Se più
    /// motivi si applicano insieme (es. un residente minorenne), vince il più favorevole all'ospite
    /// (percentuale più alta), non si sommano. Il checkbox manuale "Esente da tassa" resta sempre
    /// 100% pieno — copre casi che non hanno una percentuale configurabile propria (disabili, forze
    /// dell'ordine, ecc., vedi la categoria PayTourist "Esenzione").
    /// </summary>
    private static decimal PercentualeRiduzione(bool esenteManuale, string? luogoResidenza, DateTime? dataNascita, string? comuneStruttura, DateTime checkIn, ImpostazioniStruttura impostazioni)
    {
        decimal percentuale = esenteManuale ? 100m : 0m;

        if (EsenteResidenza(luogoResidenza, comuneStruttura))
        {
            percentuale = Math.Max(percentuale, impostazioni.TassaSoggiornoPercentualeResidenti ?? 100m);
        }

        if (PercentualeEta(dataNascita, checkIn, impostazioni) is { } percentualeEta)
        {
            percentuale = Math.Max(percentuale, percentualeEta);
        }

        return percentuale;
    }

    /// <summary>
    /// Soglie configurate in Impostazioni (età compiuta al check-in) — vedi
    /// <see cref="ImpostazioniStruttura.TassaSoggiornoEtaEsenzioneMinori"/>. Nessuna soglia
    /// impostata o data di nascita mancante → null (nessuna riduzione automatica per età).
    /// </summary>
    private static decimal? PercentualeEta(DateTime? dataNascita, DateTime checkIn, ImpostazioniStruttura impostazioni)
    {
        if (dataNascita is null)
        {
            return null;
        }

        var eta = checkIn.Year - dataNascita.Value.Year;
        if (checkIn.Date < dataNascita.Value.Date.AddYears(eta))
        {
            eta--;
        }

        if (impostazioni.TassaSoggiornoEtaEsenzioneMinori is { } sogliaMinori && eta < sogliaMinori)
        {
            return impostazioni.TassaSoggiornoPercentualeMinori ?? 100m;
        }

        if (impostazioni.TassaSoggiornoEtaEsenzioneAnziani is { } sogliaAnziani && eta >= sogliaAnziani)
        {
            return impostazioni.TassaSoggiornoPercentualeAnziani ?? 100m;
        }

        return null;
    }

    /// <summary>
    /// Bug reale corretto: confrontava le stringhe intere, ma i campi Comune sono salvati come
    /// "COMUNE (PROVINCIA)" (es. "Castellammare del Golfo (TP)") — un ospite residente lì non
    /// risultava mai esente se il Comune Attività era scritto senza provincia (o viceversa). Estrae
    /// il solo nome città prima di confrontare, stesso ExtractCity già duplicato in
    /// PayTouristDtoBuilder/SchedinaAlloggiatiWebBuilder/StayBuilderOsservatorio/PayTouristClient.
    /// internal per essere testabile direttamente (vedi OspitiServiceTests).
    /// </summary>
    internal static bool EsenteResidenza(string? luogoResidenza, string? comuneStruttura)
    {
        var residenza = ExtractCity(luogoResidenza);
        var comune = ExtractCity(comuneStruttura);
        return !string.IsNullOrWhiteSpace(residenza) && !string.IsNullOrWhiteSpace(comune) && string.Equals(residenza, comune, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractCity(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        var parts = input.Split(" (");
        return parts.Length > 2 ? $"{parts[0].Trim()} ({parts[1].Trim()}" : parts[0].Trim();
    }

    private async Task<Prenotazione> GetPrenotazioneOwnedAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken)
            ?? throw new NotFoundException("Prenotazione non trovata.");
        if (prenotazione.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Prenotazione non trovata.");
        }

        return prenotazione;
    }
}
