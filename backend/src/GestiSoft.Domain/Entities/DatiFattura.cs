using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Riga/testata fattura elettronica. Porta 1:1 da
/// OrderManagement.Model.BusinesObject.DatiFattura del sistema legacy.
/// </summary>
public class DatiFattura : TenantEntity
{
    /// <summary>
    /// Prenotazione da cui è nata questa fattura — null per le fatture create prima dell'introduzione
    /// di questo campo (nessuno storico da recuperare, il legame andava già perso). Usata per mostrare
    /// "Fattura generata" sulla scheda ospiti di una prenotazione già fatturata.
    /// </summary>
    public Guid? PrenotazioneId { get; set; }

    public Guid? DatiClienteId { get; set; }

    public DatiCliente? Cliente { get; set; }

    public int Progressivo { get; set; }

    public TipoDocumentoFattura? TipoDocumento { get; set; }

    public RegimeFiscale? RegimeFiscale { get; set; }

    public int NumeroDocumento { get; set; }

    public DateTime DataDocumento { get; set; }

    public string? Divisa { get; set; }

    public string? Descrizione { get; set; }

    public decimal Quantita { get; set; }

    public decimal PrezzoUnitario { get; set; }

    public AliquotaIva? AliquotaIva { get; set; }

    public NaturaIva? Natura { get; set; }

    public decimal PrezzoTotale { get; set; }

    public decimal ImportoTotale { get; set; }

    public decimal Arrotondamento { get; set; }

    /// <summary>Percorso/URL del PDF o XML generato.</summary>
    public string? Link { get; set; }

    public int Anno { get; set; } = DateTime.UtcNow.Year;
}
