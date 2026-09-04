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
    private const int GiorniPreavvisoScadenza = 30;

    public async Task<StatisticheSuperAdminResult> GetStatisticheAsync(int anno, CancellationToken cancellationToken)
    {
        var clienti = await db.Clienti.AsNoTracking().ToListAsync(cancellationToken);
        var strutture = await db.Strutture.AsNoTracking().ToListAsync(cancellationToken);
        var wubook = await db.WubookIntegrazioni.AsNoTracking().ToListAsync(cancellationToken);
        var tokenWubookGlobale = (await db.ImpostazioniGlobali.AsNoTracking().FirstOrDefaultAsync(cancellationToken))?.TokenWubook;
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

        var incassiRinnoviPerMese = await IncassiRinnoviPerMeseAsync(anno, cancellationToken);

        var panoramica = new PanoramicaBusinessResult(
            clienti.Count(c => c.Attivo),
            clienti.Count,
            strutture.Count(s => s.Attivo),
            strutture.Count);

        var nuoviClientiPerMese = NuoviClientiPerMese(clienti, anno);

        // Una Struttura disattivata (o il cui Cliente è disattivato) non deve comparire in nessuna
        // vista della Dashboard Super Admin: non è più operativa, non ha senso sollecitarla o
        // segnalarne lo stato di salute.
        var struttureVisibili = strutture
            .Where(s => s.Attivo && clientiById.GetValueOrDefault(s.ClienteId)?.Attivo == true)
            .ToList();

        var oggi = DateTime.UtcNow;

        var licenzeScadute = struttureVisibili
            .Where(s => s.ScadenzaLicenza is null || s.ScadenzaLicenza < oggi)
            .Select(s => new LicenzaScadutaResult(s.Id, s.Nome, clientiById[s.ClienteId].RagioneSociale, s.ScadenzaLicenza))
            .ToList();

        var licenzeInScadenza = struttureVisibili
            .Where(s => s.ScadenzaLicenza is { } scadenza && scadenza >= oggi && scadenza <= oggi.AddDays(GiorniPreavvisoScadenza))
            .Select(s => new LicenzaInScadenzaResult(
                s.Id,
                s.Nome,
                clientiById[s.ClienteId].RagioneSociale,
                s.ScadenzaLicenza!.Value,
                (int)Math.Ceiling((s.ScadenzaLicenza!.Value - oggi).TotalDays)))
            .OrderBy(l => l.GiorniRimanenti)
            .ToList();

        var saluteIntegrazioni = struttureVisibili
            .Select(s => new SaluteIntegrazioneStrutturaResult(
                s.Id,
                s.Nome,
                clientiById[s.ClienteId].RagioneSociale,
                EsitoAlloggiatiWeb(s, alloggiatiWebByStruttura),
                EsitoOsservatorio(s, osservatorioByStruttura[s.Id].ToList()),
                EsitoPayTourist(s, payTouristIntegrazioneByStruttura, payTouristStruttureByStruttura[s.Id].ToList()),
                EsitoWubook(s, wubookByStruttura, tokenWubookGlobale)))
            .ToList();

        return new StatisticheSuperAdminResult(panoramica, nuoviClientiPerMese, incassiRinnoviPerMese, licenzeScadute, licenzeInScadenza, saluteIntegrazioni);
    }

    public async Task<IReadOnlyList<int>> ListaAnniConDatiAsync(CancellationToken cancellationToken) =>
        await db.Prenotazioni.AsNoTracking()
            .Select(p => p.Anno)
            .Distinct()
            .OrderByDescending(a => a)
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<IncassoRinnovoMensileResult>> IncassiRinnoviPerMeseAsync(int anno, CancellationToken cancellationToken)
    {
        var perMese = await db.RinnoviLicenza.AsNoTracking()
            .Where(r => r.CreatedAtUtc.Year == anno)
            .GroupBy(r => r.CreatedAtUtc.Month)
            .Select(g => new { Mese = g.Key, Importo = g.Sum(r => r.Importo ?? 0) })
            .ToDictionaryAsync(g => g.Mese, g => g.Importo, cancellationToken);

        return Enumerable.Range(1, 12).Select(mese => new IncassoRinnovoMensileResult(mese, perMese.GetValueOrDefault(mese))).ToList();
    }

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
    private static EsitoIntegrazioneResult EsitoWubook(Struttura struttura, IReadOnlyDictionary<Guid, WubookIntegrazione> wubookByStruttura, string? tokenWubookGlobale)
    {
        if (!struttura.WubookAbilitato)
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.NonConcesso, null, null);
        }

        if (!wubookByStruttura.TryGetValue(struttura.Id, out var w) || string.IsNullOrWhiteSpace(tokenWubookGlobale) || string.IsNullOrWhiteSpace(w.CodiceStruttura))
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.NonConfigurato, null, null);
        }

        if (struttura.ScadenzaLicenza is { } scadenza && scadenza < DateTime.UtcNow)
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.Errore, null, $"Licenza scaduta il {scadenza:dd/MM/yyyy}.");
        }

        if (!string.IsNullOrWhiteSpace(w.UltimoErrore))
        {
            return new EsitoIntegrazioneResult(EsitoIntegrazione.Errore, null, w.UltimoErrore);
        }

        // Le credenziali inserite dal Super Admin sono utilizzabili subito (nessun rinnovo/cache
        // asincrono da attendere come nel comportamento precedente) — mai più "in attesa".
        return new EsitoIntegrazioneResult(EsitoIntegrazione.Attivo, null, null);
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
