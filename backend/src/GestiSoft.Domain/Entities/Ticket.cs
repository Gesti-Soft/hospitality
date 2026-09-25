using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Richiesta di assistenza aperta da una Struttura verso lo staff GestiSoft (Super Admin). La
/// aprono il titolare del Cliente e chi gestisce gli utenti della struttura (permesso SettingUser),
/// mai un operatore qualsiasi.
/// </summary>
public class Ticket : TenantEntity
{
    /// <summary>Numero progressivo unico su tutto il sistema, quello che si cita al telefono ("ticket 42").</summary>
    public int Numero { get; set; }

    public string Oggetto { get; set; } = string.Empty;

    public StatoTicket Stato { get; set; } = StatoTicket.Aperto;

    /// <summary>Chi l'ha aperto. Null se nel frattempo l'utente è stato eliminato.</summary>
    public Guid? AutoreUtenteId { get; set; }

    public Utente? AutoreUtente { get; set; }

    public DateTime UltimoMessaggioClienteAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Null finché lo staff non ha mai risposto.</summary>
    public DateTime? UltimoMessaggioStaffAtUtc { get; set; }

    /// <summary>
    /// Ultima volta che qualcuno della struttura ha aperto il ticket: una risposta dello staff più
    /// recente di questa è "non letta". Uno solo per tutta la struttura, non per utente: chi la legge
    /// per primo la legge per tutti.
    /// </summary>
    public DateTime? LettoClienteAtUtc { get; set; }

    /// <summary>Come <see cref="LettoClienteAtUtc"/>, dal lato dello staff.</summary>
    public DateTime? LettoStaffAtUtc { get; set; }

    public DateTime? ChiusoAtUtc { get; set; }

    /// <summary>
    /// Valorizzato quando, 12 mesi dopo la chiusura, messaggi e allegati sono stati cancellati e
    /// l'autore scollegato (conservazione GDPR, vedi AssistenzaService.AnonimizzaScadutiAsync).
    /// Restano numero, oggetto, struttura, stato e date.
    /// </summary>
    public DateTime? AnonimizzatoAtUtc { get; set; }

    public ICollection<TicketMessaggio> Messaggi { get; set; } = new List<TicketMessaggio>();
}
