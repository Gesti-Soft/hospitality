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
/// del legacy (vedi report Fase 3 sez. C.3) e il calcolo tassa di soggiorno (sez. C.7, limitato
/// per ora all'importo pieno con esenzione per residenza: le riduzioni per età dipendono
/// dall'integrazione PayTourist, non ancora portata — vedi Fase 8).
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
        prenotazione.TotalTax = await CalcolaTassaSoggiornoAsync(strutturaId, prenotazione, ospite, cancellationToken);
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
    /// Tassa di soggiorno: prezzo per notte (fino al tetto massimo di notti configurato) per
    /// ogni persona non esente e non residente nel comune della struttura. Le riduzioni per età
    /// (minori/anziani) del legacy dipendono da percentuali fornite dall'integrazione PayTourist
    /// (Fase 8) e non sono ancora applicate qui — TODO quando quell'integrazione sarà portata.
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
        foreach (var (esente, luogoResidenza) in PersoneScheda(ospite))
        {
            if (esente || EsenteResidenza(luogoResidenza, comuneStruttura))
            {
                continue;
            }

            totale += importoPerPersona;
        }

        return totale;
    }

    private static IEnumerable<(bool EsenteDaTassa, string? LuogoResidenza)> PersoneScheda(Ospite ospite)
    {
        yield return (ospite.EsenteDaTassa, ospite.LuogoResidenza);
        foreach (var membro in ospite.Membri)
        {
            yield return (membro.EsenteDaTassa, membro.LuogoResidenza);
        }
    }

    private static bool EsenteResidenza(string? luogoResidenza, string? comuneStruttura) =>
        !string.IsNullOrWhiteSpace(luogoResidenza)
        && !string.IsNullOrWhiteSpace(comuneStruttura)
        && string.Equals(luogoResidenza.Trim(), comuneStruttura.Trim(), StringComparison.OrdinalIgnoreCase);

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
