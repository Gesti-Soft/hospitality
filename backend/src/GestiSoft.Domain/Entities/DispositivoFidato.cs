namespace GestiSoft.Domain.Entities;

/// <summary>
/// Browser su cui il codice è già stato inserito e non viene richiesto di nuovo fino alla scadenza
/// ("ricorda questo dispositivo"). Il token vero vive solo nel browser; qui è in hash, così chi
/// legge la tabella non può costruirsi un dispositivo fidato.
/// </summary>
public class DispositivoFidato
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UtenteId { get; set; }

    public Utente? Utente { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ScadeAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
