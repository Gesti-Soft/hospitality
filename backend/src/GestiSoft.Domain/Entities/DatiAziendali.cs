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

    public string? Indirizzo { get; set; }

    public string? NCivico { get; set; }

    public string? Cap { get; set; }

    public string? Comune { get; set; }

    public string? Provincia { get; set; }

    public string? Nazione { get; set; }
}
