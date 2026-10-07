using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Una consegna dello SdI sul servizio di ricezione: il file di un fornitore com'è arrivato, prima
/// di capire cosa contiene e di chi è. Registrare prima, capire poi: ogni consegna lascia una riga
/// con i byte originali anche quando non si legge o non si sa a quale struttura appartiene, e le
/// fatture ricevute nascono da qui. Una consegna ripetuta dallo SdI non crea una seconda riga:
/// (IdentificativoSdi, Prova) è unico.
/// </summary>
public class FileRicevutoSdi : Entity
{
    public string IdentificativoSdi { get; set; } = string.Empty;

    /// <summary>Arrivato sull'endpoint di collaudo: documento inventato, mai attribuito a una struttura.</summary>
    public bool Prova { get; set; }

    public string NomeFile { get; set; } = string.Empty;

    /// <summary>I byte esatti ricevuti (XML, XML firmato .p7m, o zip).</summary>
    public byte[] File { get; set; } = [];

    public string? NomeFileMetadati { get; set; }

    public byte[]? Metadati { get; set; }

    /// <summary>La struttura destinataria, trovata dal codice fiscale o dalla partita IVA del cessionario. Null in quarantena.</summary>
    public Guid? StrutturaId { get; set; }

    /// <summary>Partita IVA o codice fiscale del cessionario letti dal file: serve ad attribuirlo a mano.</summary>
    public string? IdentificativoCessionario { get; set; }

    /// <summary>Denominazione del fornitore letta dal file, per riconoscerlo in quarantena.</summary>
    public string? Fornitore { get; set; }

    public EsitoFileRicevutoSdi Esito { get; set; }

    public string? Messaggio { get; set; }

    public DateTime RicevutoAtUtc { get; set; } = DateTime.UtcNow;
}
