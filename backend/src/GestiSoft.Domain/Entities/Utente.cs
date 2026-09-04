namespace GestiSoft.Domain.Entities;

/// <summary>
/// Un utente del gestionale. Globale (non scoped per Struttura): un Super Admin non appartiene
/// a nessun Cliente, un utente normale appartiene a un Cliente e vede le Strutture per cui ha
/// un ruolo assegnato in UtenteStruttura. Porta 1:1 il concetto di
/// OrderManagement.Model.BusinesObject.User del sistema legacy, ma con Email al posto di
/// UserName (univoca globalmente, più adatta a un sistema multi-tenant unico) e password
/// hashata (il legacy la salvava in chiaro).
/// </summary>
public class Utente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Nome { get; set; }

    public string? Cognome { get; set; }

    public bool IsSuperAdmin { get; set; }

    /// <summary>Null solo per i Super Admin, che non appartengono a un Cliente.</summary>
    public Guid? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public bool Attivo { get; set; } = true;

    /// <summary>
    /// True solo per il titolare/account Cliente: accesso libero a tutte le Strutture del proprio
    /// Cliente (comprese quelle non ancora assegnategli in UtenteStruttura), permessi granulari
    /// sempre concessi, senza bisogno di alcuna riga UtenteStruttura. Un utente normale (dipendente
    /// di una Struttura) resta invece limitato alle sole Strutture a cui è stato assegnato.
    /// </summary>
    public bool IsClienteAccount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UtenteStruttura> Strutture { get; set; } = new List<UtenteStruttura>();
}
