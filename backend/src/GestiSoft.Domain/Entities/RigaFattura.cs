using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Una riga di fattura o di ricevuta: il soggiorno, un servizio extra, una riga scritta a mano. Ogni
/// riga ha la sua aliquota IVA, o la natura che dice perché non c'è: la SPA può essere al 22% e
/// l'alloggio al 10% sulla stessa fattura. L'imposta di soggiorno non è una riga: sta sulla testata
/// (DatiFattura.ImpostaSoggiorno) e si scrive a parte con natura N1.
/// </summary>
public class RigaFattura : TenantEntity
{
    public Guid DatiFatturaId { get; set; }

    /// <summary>Ordine sulla fattura, da 1.</summary>
    public int Numero { get; set; }

    public string Descrizione { get; set; } = string.Empty;

    public decimal Quantita { get; set; } = 1;

    public decimal PrezzoUnitario { get; set; }

    /// <summary>Quantità × prezzo unitario, arrotondato al centesimo: è l'imponibile della riga.</summary>
    public decimal PrezzoTotale { get; set; }

    /// <summary>Null su una ricevuta, o quando c'è la natura.</summary>
    public AliquotaIva? AliquotaIva { get; set; }

    public NaturaIva? Natura { get; set; }

    public TipoRigaFattura Tipo { get; set; } = TipoRigaFattura.Altro;

    /// <summary>La prenotazione da cui viene la riga: una fattura può averne più d'una (due famiglie, paga una).</summary>
    public Guid? PrenotazioneId { get; set; }

    /// <summary>Il servizio extra fatturato, per non fatturarlo una seconda volta.</summary>
    public Guid? PrenotazioneServizioId { get; set; }
}
