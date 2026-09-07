using GestiSoft.Application.Statistiche;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class StatisticheRepository(GestiSoftDbContext db) : IStatisticheRepository
{
    public async Task<IReadOnlyList<int>> ListaAnniConDatiAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.Prenotazioni.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId)
            .Select(p => p.Anno)
            .Distinct()
            .OrderByDescending(a => a)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Su segnalazione esplicita dell'utente: una prenotazione Annullata non è mai stato un soggiorno
    /// reale, non deve contare come "prenotazione" nel KPI né nelle sue percentuali derivate (vedi
    /// StatisticheService.GetStatisticheAsync, che usa questo conteggio anche come denominatore per
    /// ContaPerNazionalitaAsync) — stessa esclusione già applicata a SommaTassaSoggiornoAnnoAsync,
    /// estesa qui e alle altre query "conteggio prenotazioni" di questo repository per coerenza.
    /// </summary>
    public Task<int> ContaPrenotazioniAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken) =>
        db.Prenotazioni.AsNoTracking()
            .CountAsync(p => p.StrutturaId == strutturaId && p.Anno == anno && p.StatoPrenotazione != StatoPrenotazione.Annullata, cancellationToken);

    public async Task<(decimal Stimato, decimal Effettivo)> SommaImportiAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        var risultato = await db.Prenotazioni.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId && p.Anno == anno)
            .GroupBy(_ => 1)
            .Select(g => new { Stimato = g.Sum(p => p.ImportoTotale ?? 0), Effettivo = g.Sum(p => p.ImportoPagato ?? 0) })
            .FirstOrDefaultAsync(cancellationToken);

        return (risultato?.Stimato ?? 0, risultato?.Effettivo ?? 0);
    }

    public async Task<double?> PermanenzaMediaAsync(Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        var risultato = await db.Ospiti.AsNoTracking()
            .Where(o => o.StrutturaId == strutturaId && o.Prenotazione != null && o.Prenotazione.Anno == anno && o.Permanenza > 0)
            .GroupBy(_ => 1)
            .Select(g => new { Conteggio = g.Count(), Somma = g.Sum(o => o.Permanenza!.Value) })
            .FirstOrDefaultAsync(cancellationToken);

        return risultato is { Conteggio: > 0 } ? (double)risultato.Somma / risultato.Conteggio : null;
    }

    public Task<int> SommaPermanenzaAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken) =>
        db.Ospiti.AsNoTracking()
            .Where(o => o.StrutturaId == strutturaId && o.Prenotazione != null && o.Prenotazione.Anno == anno && o.Permanenza > 0)
            .SumAsync(o => o.Permanenza!.Value, cancellationToken);

    public async Task<IReadOnlyList<(string? Etichetta, int Conteggio)>> ContaPerAgenziaAsync(Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        var righe = await db.Prenotazioni.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId && p.Anno == anno && p.StatoPrenotazione != StatoPrenotazione.Annullata)
            .GroupBy(p => p.Agenzia)
            .Select(g => new { Etichetta = g.Key, Conteggio = g.Count() })
            .ToListAsync(cancellationToken);

        return righe.Select(r => (r.Etichetta, r.Conteggio)).ToList();
    }

    public async Task<IReadOnlyList<(string? Etichetta, int Conteggio)>> ContaPerNazionalitaAsync(Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        var righe = await db.Ospiti.AsNoTracking()
            .Where(o => o.StrutturaId == strutturaId && o.Prenotazione != null && o.Prenotazione.Anno == anno
                && o.Prenotazione.StatoPrenotazione != StatoPrenotazione.Annullata)
            .GroupBy(o => o.Cittadinanza)
            .Select(g => new { Etichetta = g.Key, Conteggio = g.Count() })
            .ToListAsync(cancellationToken);

        return righe.Select(r => (r.Etichetta, r.Conteggio)).ToList();
    }

    public async Task<IReadOnlyList<(int Mese, decimal Totale)>> RicavoMensileAsync(Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        var righe = await db.Prenotazioni.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId && p.CheckIn != null && p.CheckIn.Value.Year == anno)
            .GroupBy(p => p.CheckIn!.Value.Month)
            .Select(g => new { Mese = g.Key, Totale = g.Sum(p => p.ImportoPagato ?? 0) })
            .ToListAsync(cancellationToken);

        return righe.Select(r => (r.Mese, r.Totale)).ToList();
    }

    public async Task<IReadOnlyList<(string Tipologia, decimal Totale)>> RicavoPerTipologiaAsync(Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        var righe = await db.Prenotazioni.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId && p.CheckIn != null && p.CheckIn.Value.Year == anno && p.Camera != null && p.Camera.Tipologia != null)
            .GroupBy(p => p.Camera!.Tipologia!.TipologiaCamera)
            .Select(g => new { Tipologia = g.Key, Totale = g.Sum(p => p.ImportoPagato ?? 0) })
            .ToListAsync(cancellationToken);

        return righe.Select(r => (r.Tipologia, r.Totale)).ToList();
    }

    public async Task<IReadOnlyList<(string Tipologia, int Mese, int Conteggio)>> PrenotazioniPerTipologiaMeseAsync(Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        var righe = await db.Prenotazioni.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId && p.CheckIn != null && p.CheckIn.Value.Year == anno && p.Camera != null && p.Camera.Tipologia != null
                && p.StatoPrenotazione != StatoPrenotazione.Annullata)
            .GroupBy(p => new { Tipologia = p.Camera!.Tipologia!.TipologiaCamera, Mese = p.CheckIn!.Value.Month })
            .Select(g => new { g.Key.Tipologia, g.Key.Mese, Conteggio = g.Count() })
            .ToListAsync(cancellationToken);

        return righe.Select(r => (r.Tipologia, r.Mese, r.Conteggio)).ToList();
    }

    /// <summary>
    /// Solo prenotazioni già InCorso o Completata (ospite arrivato) — su segnalazione esplicita, una
    /// prenotazione ancora Incompleta (check-in non ancora fatto) non deve contribuire al totale: è
    /// tassa non ancora dovuta/confermata, sommarla darebbe un dato falsato. Annullata esclusa per lo
    /// stesso motivo (mai stata realmente dovuta).
    /// </summary>
    public Task<decimal> SommaTassaSoggiornoAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken) =>
        db.Prenotazioni.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId
                && p.Anno == anno
                && (p.StatoPrenotazione == StatoPrenotazione.InCorso || p.StatoPrenotazione == StatoPrenotazione.Completata))
            .SumAsync(p => p.TotalTax ?? 0, cancellationToken);

    /// <summary>Stessa restrizione InCorso/Completata di <see cref="SommaTassaSoggiornoAnnoAsync"/>, per coerenza.</summary>
    public async Task<IReadOnlyList<(int Mese, decimal Totale)>> TassaSoggiornoMensileAsync(Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        var righe = await db.Prenotazioni.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId
                && p.CheckIn != null && p.CheckIn.Value.Year == anno
                && (p.StatoPrenotazione == StatoPrenotazione.InCorso || p.StatoPrenotazione == StatoPrenotazione.Completata))
            .GroupBy(p => p.CheckIn!.Value.Month)
            .Select(g => new { Mese = g.Key, Totale = g.Sum(p => p.TotalTax ?? 0) })
            .ToListAsync(cancellationToken);

        return righe.Select(r => (r.Mese, r.Totale)).ToList();
    }
}
