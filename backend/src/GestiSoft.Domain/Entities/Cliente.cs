namespace GestiSoft.Domain.Entities;

/// <summary>
/// Il gestore/azienda cliente di GestiSoft — radice reale del tenant. Un Cliente può avere
/// più Strutture (proprietà). Un Cliente vede solo le proprie Strutture; solo il Super Admin
/// vede tutti i Clienti.
/// </summary>
public class Cliente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RagioneSociale { get; set; } = string.Empty;

    public string? PartitaIva { get; set; }

    /// <summary>
    /// Sospensione dell'intero Cliente da parte del Super Admin (es. mancato pagamento): se false,
    /// nessun utente di questo Cliente può accedere (AuthService.LoginAsync) né operare su nessuna
    /// delle sue Strutture (TenantAccessGuard.EnsureAccessAsync), anche con un token già emesso.
    /// </summary>
    public bool Attivo { get; set; } = true;

    /// <summary>Quota mensile pattuita con il Cliente — solo un promemoria per il Super Admin, non genera fatture né blocca l'accesso da sola.</summary>
    public decimal? QuotaMensile { get; set; }

    /// <summary>Appunti liberi del Super Admin su questo Cliente (es. accordi commerciali, contatti) — mai visibile al Cliente stesso.</summary>
    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Struttura> Strutture { get; set; } = new List<Struttura>();
}
