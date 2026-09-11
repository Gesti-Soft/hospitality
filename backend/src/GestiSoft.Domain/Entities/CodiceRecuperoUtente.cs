namespace GestiSoft.Domain.Entities;

/// <summary>
/// Codice di recupero usa e getta per rientrare quando il telefono con Google Authenticator non
/// c'è più. Generati in blocco all'attivazione del 2FA e mostrati una sola volta: qui se ne
/// conserva solo l'hash, esattamente come per le password — se il database finisse in mani
/// altrui, non sarebbero comunque una scorciatoia per entrare.
/// </summary>
public class CodiceRecuperoUtente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UtenteId { get; set; }

    public Utente? Utente { get; set; }

    public string CodiceHash { get; set; } = string.Empty;

    /// <summary>Valorizzato quando il codice viene speso: da quel momento non vale più.</summary>
    public DateTime? UsatoAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
