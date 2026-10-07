using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Un messaggio dello SdI su una fattura trasmessa (consegna, scarto, mancata consegna…), conservato
/// com'è arrivato: dal canale una ricevuta non si riscarica. Si registra anche quando non si riesce
/// a leggere o non si trova la fattura: allora resta senza struttura e la vede il Super Admin. Dal
/// canale non c'è nessun utente collegato, quindi la struttura è quella della fattura trovata.
/// </summary>
public class RicevutaSdi : Entity
{
    /// <summary>Null finché la ricevuta non si aggancia a una fattura.</summary>
    public Guid? StrutturaId { get; set; }

    public Guid? TrasmissioneFatturaId { get; set; }

    public TipoRicevutaSdi Tipo { get; set; }

    public string? IdentificativoSdi { get; set; }

    /// <summary>Il nome del file della fattura a cui si riferisce: è il dato con cui si aggancia.</summary>
    public string? NomeFileFattura { get; set; }

    /// <summary>Il nome del file della ricevuta, come l'ha mandato lo SdI.</summary>
    public string? NomeFile { get; set; }

    /// <summary>Il file grezzo, firmato XAdES. Conservato anche se illeggibile.</summary>
    public byte[] Xml { get; set; } = [];

    /// <summary>Arrivata sull'endpoint di collaudo: si conserva e si mostra, non tocca mai una fattura.</summary>
    public bool Prova { get; set; }

    /// <summary>Esito in parole: per uno scarto, codici e descrizioni degli errori.</summary>
    public string? Descrizione { get; set; }

    public DateTime RicevutaAtUtc { get; set; } = DateTime.UtcNow;
}
