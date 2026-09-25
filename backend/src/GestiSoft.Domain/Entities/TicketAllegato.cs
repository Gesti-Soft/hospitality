using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Foto allegata a un messaggio di assistenza. Il file sta su disco, non nel database: alla
/// chiusura del ticket si cancella e lo spazio torna libero subito, e nel frattempo non finisce nei
/// backup del database — uno screenshot del gestionale può mostrare i dati degli ospiti. La riga
/// resta, con <see cref="EliminatoAtUtc"/> valorizzato, perché la conversazione dica che lì c'era una
/// foto.
/// </summary>
public class TicketAllegato : TenantEntity
{
    public Guid MessaggioId { get; set; }

    public TicketMessaggio? Messaggio { get; set; }

    public string NomeFile { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long DimensioneByte { get; set; }

    /// <summary>Percorso relativo alla cartella degli allegati, generato dal server (mai dal nome caricato).</summary>
    public string Percorso { get; set; } = string.Empty;

    public DateTime? EliminatoAtUtc { get; set; }
}
