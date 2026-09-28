using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione;

/// <summary>Un gruppo del riepilogo IVA: tutte le righe con la stessa aliquota, o la stessa natura.</summary>
public record RiepilogoIva(AliquotaIva? AliquotaIva, NaturaIva? Natura, decimal Imponibile, decimal Imposta);

/// <summary>
/// I conti di una fattura a righe, in un posto solo: li usano il servizio (totale e bollo salvati),
/// il PDF e l'XML, che devono dire la stessa cifra. L'IVA si calcola sul totale di ogni aliquota e
/// non riga per riga, come fa lo SdI nel riepilogo: una differenza di un centesimo tra i due farebbe
/// scartare il file.
/// </summary>
public static class CalcoloFattura
{
    /// <summary>Sopra questa soglia le somme non soggette a IVA scontano il bollo (art. 13 Tariffa DPR 642/72).</summary>
    public const decimal SogliaBollo = 77.47m;

    public const decimal ImportoBolloVirtuale = 2.00m;

    /// <summary>
    /// Regola del tracciato: con un'aliquota maggiore di zero niente natura; con IVA a 0% (o senza
    /// aliquota) serve la natura, cioè il motivo per cui l'IVA non c'è. Si salva sempre come 0% più
    /// natura, la convenzione già usata dalle fatture esistenti.
    /// </summary>
    public static (AliquotaIva? Aliquota, NaturaIva? Natura) AliquotaENatura(AliquotaIva? aliquota, NaturaIva? natura, string riferimento)
    {
        if (aliquota is { } a && a != Domain.Enums.AliquotaIva.Iva0)
        {
            return natura is null
                ? (a, null)
                : throw new Exceptions.ConflictException($"{riferimento}: con l'IVA al {(int)a}% non va indicata la natura.");
        }

        return natura is { } n
            ? (Domain.Enums.AliquotaIva.Iva0, n)
            : throw new Exceptions.ConflictException($"{riferimento}: con l'IVA a 0% indica la natura, il motivo per cui l'IVA non c'è.");
    }

    public static decimal TotaleRiga(decimal quantita, decimal prezzoUnitario) =>
        Math.Round(quantita * prezzoUnitario, 2, MidpointRounding.AwayFromZero);

    public static IReadOnlyList<RiepilogoIva> Riepiloghi(IEnumerable<RigaFattura> righe) =>
        righe
            .GroupBy(r => (r.AliquotaIva, r.Natura))
            .OrderByDescending(g => g.Key.AliquotaIva is { } a ? (int)a : -1)
            .ThenBy(g => g.Key.Natura is { } n ? (int)n : 0)
            .Select(g =>
            {
                var imponibile = g.Sum(r => r.PrezzoTotale);
                var imposta = g.Key.AliquotaIva is { } aliquota
                    ? Math.Round(imponibile * (int)aliquota / 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                return new RiepilogoIva(g.Key.AliquotaIva, g.Key.Natura, imponibile, imposta);
            })
            .ToList();

    public static decimal Imponibile(IEnumerable<RigaFattura> righe) => righe.Sum(r => r.PrezzoTotale);

    public static decimal Imposta(IEnumerable<RigaFattura> righe) => Riepiloghi(righe).Sum(r => r.Imposta);

    /// <summary>Imponibile + IVA + imposta di soggiorno (che entra nel totale da pagare ma non è IVA).</summary>
    public static decimal Totale(IReadOnlyList<RigaFattura> righe, decimal impostaSoggiorno) =>
        Imponibile(righe) + Imposta(righe) + impostaSoggiorno;

    /// <summary>
    /// IVA e bollo sono alternativi (art. 6 Tabella B DPR 642/72): dove c'è IVA il bollo non si paga
    /// mai, dove non c'è si paga sopra 77,47 €. La soglia si misura sulla sola parte <b>non</b>
    /// soggetta: le righe con una natura (o tutte, su una ricevuta di locazione breve, che è interamente
    /// fuori campo IVA) più l'imposta di soggiorno esclusa art. 15. In una fattura mista — alloggio al
    /// 10% e imposta di soggiorno — conta solo la seconda: un hotel ordinario il bollo non lo paga quasi
    /// mai, un forfettario quasi sempre.
    /// </summary>
    public static decimal? Bollo(TipoEmissioneDocumento tipoEmissione, IEnumerable<RigaFattura> righe, decimal impostaSoggiorno)
    {
        var ricevuta = tipoEmissione == TipoEmissioneDocumento.Ricevuta;
        var nonSoggetto = righe.Where(r => ricevuta || r.Natura is not null).Sum(r => r.PrezzoTotale) + impostaSoggiorno;
        return nonSoggetto > SogliaBollo ? ImportoBolloVirtuale : null;
    }
}
