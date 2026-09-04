using GestiSoft.Application.Statistiche;
using GestiSoft.Application.SuperAdmin;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

/// <summary>
/// Stesso principio di SuperAdminRepository.GetDashboardAsync: Clienti/Strutture/righe di
/// integrazione (volumi da pannello di amministrazione) caricati in memoria e ricomposti con
/// dizionari; Prenotazioni/Ospiti (potenzialmente molte righe su tutti i tenant) sempre aggregati in
/// SQL per Struttura (GroupBy(StrutturaId), mai caricate intere), poi ricongiunti in memoria al
/// piccolo dizionario Struttura→Cliente.
/// </summary>
public class StatisticheSuperAdminRepository(GestiSoftDbContext db) : IStatisticheSuperAdminRepository
{
    private const int MassimoVociClassifica = 10;

    public async Task<StatisticheSuperAdminResult> GetStatisticheAsync(int anno, CancellationToken cancellationToken)
    {
        var clienti = await db.Clienti.AsNoTracking().ToListAsync(cancellationToken);
        var strutture = await db.Strutture.AsNoTracking().ToListAsync(cancellationToken);
        var wubook = await db.WubookIntegrazioni.AsNoTracking().ToListAsync(cancellationToken);
        var alloggiatiWeb = await db.AlloggiatiWebIntegrazioni.AsNoTracking().ToListAsync(cancellationToken);
        var osservatorio = await db.OsservatorioAppartamenti.AsNoTracking().ToListAsync(cancellationToken);
        var payTouristIntegrazioni = await db.PayTouristIntegrazioni.AsNoTracking().ToListAsync(cancellationToken);
        var payTouristStrutture = await db.PayTouristStrutture.AsNoTracking().ToListAsync(cancellationToken);

        var clientiById = clienti.ToDictionary(c => c.Id);
        var wubookByStruttura = wubook.ToDictionary(w => w.StrutturaId);
        var alloggiatiWebByStruttura = alloggiatiWeb.ToDictionary(a => a.StrutturaId);
        var osservatorioByStruttura = osservatorio.ToLookup(o => o.StrutturaId);
        var payTouristIntegrazioneByStruttura = payTouristIntegrazioni.ToDictionary(p => p.StrutturaId);
        var payTouristStruttureByStruttura = payTouristStrutture.ToLookup(p => p.StrutturaId);

        var importoPagatoPerStruttura = await ImportoPagatoPerStrutturaAsync(anno, cancellationToken);
        var sommaPermanenzaPerStruttura = await SommaPermanenzaPerStrutturaAsync(anno, cancellationToken);
        var numeroCamerePerStruttura = await db.Camere.AsNoTracking()
            .GroupBy(c => c.StrutturaId)
            .Select(g => new { StrutturaId = g.Key, Conteggio = g.Count() })
            .ToDictionaryAsync(g => g.StrutturaId, g => g.Conteggio, cancellationToken);

        var panoramica = new PanoramicaBusinessResult(
            clienti.Count(c => c.Attivo),
            clienti.Count,
            strutture.Count(s => s.Attivo),
            strutture.Count);

        var nuoviClientiPerMese = NuoviClientiPerMese(clienti, anno);

        var incassiPerCliente = strutture
            .GroupBy(s => s.ClienteId)
            .Select(g => new
            {
                ClienteId = g.Key,
                Importo = g.Sum(s => importoPagatoPerStruttura.GetValueOrDefault(s.Id)),
                NumeroStrutture = g.Count(),
            })
            .Where(x => clientiById.ContainsKey(x.ClienteId))
            .OrderByDescending(x => x.Importo)
            .Select(x => new IncassoPerClienteResult(x.ClienteId, clientiById[x.ClienteId].RagioneSociale, x.Importo, x.NumeroStrutture))
            .ToList();

        var classificaFatturato = strutture
            .Where(s => clientiById.ContainsKey(s.ClienteId))
            .Select(s => new ClassificaStrutturaResult(s.Id, s.Nome, clientiById[s.ClienteId].RagioneSociale, importoPagatoPerStruttura.GetValueOrDefault(s.Id)))
            .OrderByDescending(c => c.Valore)
            .Take(MassimoVociClassifica)
            .ToList();

        var classificaOccupazione = strutture
            .Where(s => clientiById.ContainsKey(s.ClienteId))
            .Select(s => new ClassificaStrutturaResult(
                s.Id,
                s.Nome,
                clientiById[s.ClienteId].RagioneSociale,
                (decimal)CalcoloOccupazione.TassoOccupazionePercentuale(
                    sommaPermanenzaPerStruttura.GetValueOrDefault(s.Id),
                    numeroCamerePerStruttura.GetValueOrDefault(s.Id),
                    anno)))
            .OrderByDescending(c => c.Valore)
            .Take(MassimoVociClassifica)
            .ToList();

        var saluteIntegrazioni = strutture
            .Where(s => clientiById.ContainsKey(s.ClienteId))
            .Select(s => new SaluteIntegrazioneStrutturaResult(
                s.Id,
                s.Nome,
                clientiById[s.ClienteId].RagioneSociale,
                EsitoAlloggiatiWeb(s, alloggiatiWebByStruttura),
                EsitoOsservatorio(s, osservatorioByStruttura[s.Id].ToList()),
                EsitoPayTourist(s, payTouristIntegrazioneByStruttura, payTouristStruttureByStruttura[s.Id].ToList()),
                EsitoWubook(s, wubookByStruttura)))
            .ToList();

        return new StatisticheSuperAdminResult(panoramica, nuoviClientiPerMese, incassiPerCliente, classificaFatturato, classificaOccupazione, saluteIntegrazioni);
    }

    public async Task<IReadOnlyList<int>> ListaAnniConDatiAsync(CancellationToken cancellationToken) =>
        await db.Prenotazioni.AsNoTracking()
            .Select(p => p.Anno)
            .Distinct()
            .OrderByDescending(a => a)
            .ToListAsync(cancellationToken);

    private async Task<Dictionary<Guid, decimal>> ImportoPagatoPerStrutturaAsync(int anno, CancellationToken cancellationToken) =>
        await db.Prenotazioni.AsNoTracking()
            .Where(p => p.Anno == anno)
            .GroupBy(p => p.StrutturaId)
            .Select(g => new { StrutturaId = g.Key, Totale = g.Sum(p => p.ImportoPagato ?? 0) })
            .ToDictionaryAsync(g => g.StrutturaId, g => g.Totale, cancellationToken);

    private async Task<Dictionary<Guid, int>> SommaPermanenzaPerStrutturaAsync(int anno, CancellationToken cancellationToken) =>
        await db.Ospiti.AsNoTracking()
            .Where(o => o.Prenotazione != null && o.Prenotazione.Anno == anno && o.Permanenza > 0)
            .GroupBy(o => o.StrutturaId)
            .Select(g => new { StrutturaId = g.Key, Somma = g.Sum(o => o.Permanenza!.Value) })
            .ToDictionaryAsync(g => g.StrutturaId, g => g.Somma, cancellationToken);

    private static IReadOnlyList<TrendMensileResult> NuoviClientiPerMese(IReadOnlyList<Cliente> clienti, int anno)
    {
        var perMese = clienti
            .Where(c => c.CreatedAtUtc.Year == anno)
            .GroupBy(c => c.CreatedAtUtc.Month)
            .ToDictionary(g => g.Key, g => g.Count());

        return Enumerable.Range(1, 12).Select(mese => new TrendMensileResult(mese, perMese.GetValueOrDefault(mese))).ToList();
    }

    /// <summary>
    /// Stesse 4 casistiche già mostrate una per una in DashboardPage.tsx per la struttura
    /// dell'utente loggato (licenzaConfigurata/credenzialiConfigurate → non-configurato,
    /// UltimoErrore → errore, UltimoInvioAtUtc/UltimaVerificaOkAtUtc → ok, altrimenti attesa) più il
    /// caso NonConcesso (qui necessario: la dashboard per-struttura filtra a monte i servizi non
    /// concessi, questa vista cross-struttura no).
    /// </summary>
    private static EsitoIntegrazioneResult EsitoWubook(Struttura struttura, IReadOnlyDictionary<Guid, WubookIntegrazione> wubookByStruttura)
    {
        if (!struttura.WubookAbilitato)
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.NonConcesso, null, null);
        }

        if (!wubookByStruttura.TryGetValue(struttura.Id, out var w) || string.IsNullOrWhiteSpace(w.GestisoftUsername) || string.IsNullOrWhiteSpace(w.GestisoftToken))
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.NonConfigurato, null, null);
        }

        if (!string.IsNullOrWhiteSpace(w.UltimoErrore))
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.Errore, null, w.UltimoErrore);
        }

        return new EsitoIntegrazioneResult(w.CacheAggiornataAtUtc != null ? EsitoIntegrazione.Attivo : EsitoIntegrazione.Attesa, w.CacheAggiornataAtUtc, null);
    }

    private static EsitoIntegrazioneResult EsitoAlloggiatiWeb(Struttura struttura, IReadOnlyDictionary<Guid, AlloggiatiWebIntegrazione> byStruttura)
    {
        if (!struttura.AlloggiatiWebAbilitato)
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.NonConcesso, null, null);
        }

        if (!byStruttura.TryGetValue(struttura.Id, out var a) || string.IsNullOrWhiteSpace(a.Utente) || string.IsNullOrWhiteSpace(a.Password) || string.IsNullOrWhiteSpace(a.WsKey))
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.NonConfigurato, null, null);
        }

        if (!string.IsNullOrWhiteSpace(a.UltimoErrore))
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.Errore, null, a.UltimoErrore);
        }

        var ultimoInvio = a.UltimoInvioAtUtc ?? a.UltimaVerificaOkAtUtc;
        return new EsitoIntegrazioneResult(ultimoInvio != null ? EsitoIntegrazione.Attivo : EsitoIntegrazione.Attesa, ultimoInvio, null);
    }

    private static EsitoIntegrazioneResult EsitoOsservatorio(Struttura struttura, IReadOnlyList<OsservatorioAppartamento> appartamenti)
    {
        if (!struttura.OsservatorioAbilitato)
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.NonConcesso, null, null);
        }

        if (appartamenti.Count == 0)
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.NonConfigurato, null, null);
        }

        var inErrore = appartamenti.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.UltimoErrore));
        if (inErrore is not null)
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.Errore, null, inErrore.UltimoErrore);
        }

        var tuttiAttivi = appartamenti.All(a => a.UltimoInvioAtUtc != null || a.UltimaVerificaOkAtUtc != null);
        var ultimoInvio = appartamenti.Select(a => a.UltimoInvioAtUtc ?? a.UltimaVerificaOkAtUtc).Where(d => d != null).OrderByDescending(d => d).FirstOrDefault();
        return new EsitoIntegrazioneResult(tuttiAttivi ? EsitoIntegrazione.Attivo : EsitoIntegrazione.Attesa, ultimoInvio, null);
    }

    private static EsitoIntegrazioneResult EsitoPayTourist(
        Struttura struttura,
        IReadOnlyDictionary<Guid, PayTouristIntegrazione> integrazioneByStruttura,
        IReadOnlyList<PayTouristStruttura> struttureConfigurate)
    {
        if (!struttura.PayTouristAbilitato)
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.NonConcesso, null, null);
        }

        var tokenConfigurato = integrazioneByStruttura.TryGetValue(struttura.Id, out var config) && !string.IsNullOrWhiteSpace(config.Token);
        if (!tokenConfigurato || struttureConfigurate.Count == 0)
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.NonConfigurato, null, null);
        }

        var inErrore = struttureConfigurate.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.UltimoErrore));
        if (inErrore is not null)
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.Errore, null, inErrore.UltimoErrore);
        }

        var tuttiAttivi = struttureConfigurate.All(s => s.UltimoInvioAtUtc != null || s.UltimaVerificaOkAtUtc != null);
        var ultimoInvio = struttureConfigurate.Select(s => s.UltimoInvioAtUtc ?? s.UltimaVerificaOkAtUtc).Where(d => d != null).OrderByDescending(d => d).FirstOrDefault();
        return new EsitoIntegrazioneResult(tuttiAttivi ? EsitoIntegrazione.Attivo : EsitoIntegrazione.Attesa, ultimoInvio, null);
    }
}
