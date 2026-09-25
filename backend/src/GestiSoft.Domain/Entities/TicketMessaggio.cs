using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

public class TicketMessaggio : TenantEntity
{
    public Guid TicketId { get; set; }

    public Ticket? Ticket { get; set; }

    /// <summary>True se l'ha scritto lo staff GestiSoft (Super Admin), false se la struttura.</summary>
    public bool DaStaff { get; set; }

    public Guid? AutoreUtenteId { get; set; }

    public Utente? AutoreUtente { get; set; }

    public string Testo { get; set; } = string.Empty;

    public ICollection<TicketAllegato> Allegati { get; set; } = new List<TicketAllegato>();
}
