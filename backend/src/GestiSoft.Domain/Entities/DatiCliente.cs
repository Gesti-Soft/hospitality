using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Anagrafica del destinatario fattura (cliente/ospite fatturato). Porta 1:1 da
/// OrderManagement.Model.BusinesObject.DatiCliente del sistema legacy.
/// </summary>
public class DatiCliente : TenantEntity
{
    public string? Iso2 { get; set; }

    public string? PIva { get; set; }

    public string? CodiceFiscale { get; set; }

    public string? Denominazione { get; set; }

    public string? Nome { get; set; }

    public string? Cognome { get; set; }

    public string? Indirizzo { get; set; }

    public string? NCivico { get; set; }

    public string? Cap { get; set; }

    public string? LuogoResidenza { get; set; }

    public string? Provincia { get; set; }

    public string? Cittadinanza { get; set; }

    /// <summary>Codice destinatario SDI.</summary>
    public string? CodiceDestinatario { get; set; }

    public string? Pec { get; set; }

    public string? CustomerKey { get; set; }

    public ICollection<DatiFattura> Fatture { get; set; } = new List<DatiFattura>();
}
