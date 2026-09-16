using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Dati fiscali della struttura stessa (emittente fattura). Una riga per Struttura.
/// Porta 1:1 da OrderManagement.Model.BusinesObject.DatiAziendali del sistema legacy.
/// </summary>
public class DatiAziendali : TenantEntity
{
    public string? Iso2 { get; set; }

    public string? PIva { get; set; }

    public string? CodiceFiscale { get; set; }

    public string? Denominazione { get; set; }

    public string? Nome { get; set; }

    public string? Cognome { get; set; }

    public RegimeFiscale? RegimeFiscale { get; set; }

    /// <summary>
    /// Aliquota IVA e Natura proposte su una fattura nuova: sono una caratteristica di chi emette
    /// (il regime fiscale della struttura), non della singola fattura, quindi stanno qui e non si
    /// ridigitano ogni volta. Restano modificabili sulla fattura, dove finisce il valore effettivo.
    /// </summary>
    public AliquotaIva? AliquotaIvaDefault { get; set; }

    /// <inheritdoc cref="AliquotaIvaDefault"/>
    public NaturaIva? NaturaDefault { get; set; }

    /// <summary>
    /// La frase che per legge deve comparire in fattura quando l'IVA non si applica (regime
    /// forfettario, operazione non soggetta, esente...). È il commercialista a dettarla parola per
    /// parola e cambia da situazione a situazione: si scrive qui una volta e ogni fattura se la
    /// porta, invece di essere inventata dal software.
    /// </summary>
    public string? DicituraFattura { get; set; }

    public string? Indirizzo { get; set; }

    public string? NCivico { get; set; }

    public string? Cap { get; set; }

    public string? Comune { get; set; }

    public string? Provincia { get; set; }

    public string? Nazione { get; set; }
}
