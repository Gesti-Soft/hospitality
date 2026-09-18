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

    /// <summary>
    /// Fattura o ricevuta: decide numerazione, contenuto del PDF e se esiste un file per lo SDI.
    /// Le righe create prima di questo campo sono tutte fatture.
    /// </summary>
    public TipoEmissioneDocumento TipoEmissione { get; set; } = TipoEmissioneDocumento.Fattura;

    /// <summary>Progressivo dentro la propria serie: fatture e ricevute contano separatamente.</summary>
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

    /// <summary>
    /// Imposta di soggiorno riaddebitata all'ospite, esposta in fattura come riga a sé con natura
    /// <see cref="NaturaIva.N1_EscluseArt15"/>: è una somma anticipata in nome e per conto del
    /// cliente verso il Comune, esclusa dalla base imponibile IVA ex art. 15 c.1 n.3 DPR 633/72.
    /// Nasconderla dentro il prezzo del soggiorno la farebbe entrare nell'imponibile, cioè le
    /// farebbe pagare l'IVA che non deve.
    /// </summary>
    public decimal? ImpostaSoggiorno { get; set; }

    /// <summary>
    /// Imposta di bollo assolta in modo virtuale (2,00 €), calcolata dal sistema e conservata qui
    /// com'era al momento dell'emissione. Si applica alle sole somme <b>non</b> soggette a IVA
    /// quando superano 77,47 € (principio di alternatività IVA/bollo, art. 6 Tabella B DPR 642/72):
    /// una fattura con IVA non la paga mai, una di un forfettario quasi sempre.
    /// </summary>
    public decimal? ImportoBollo { get; set; }

    /// <summary>Percorso/URL del PDF o XML generato.</summary>
    public string? Link { get; set; }

    public int Anno { get; set; } = DateTime.UtcNow.Year;
}
