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

    /// <summary>
    /// Tentativi di login falliti consecutivi, azzerati dal primo accesso riuscito. Oltre la soglia
    /// (vedi AuthService) l'account resta bloccato per un po' — senza, il rate limit generale
    /// dell'Api lascerebbe passare decine di tentativi di password al secondo.
    /// </summary>
    public int TentativiLoginFalliti { get; set; }

    /// <summary>
    /// Fino a quando l'account rifiuta il login anche con la password giusta. Il blocco è sempre
    /// temporaneo e si scioglie da solo: uno permanente permetterebbe a chiunque conosca l'email di
    /// tenere fuori un utente a comando, sbagliando la password apposta.
    /// </summary>
    public DateTime? BloccatoFinoUtc { get; set; }

    /// <summary>
    /// Segreto TOTP in Base32 (RFC 6238), quello che finisce nel QR di Google Authenticator.
    /// Valorizzato già all'avvio dell'attivazione, quando la verifica è ancora da fare: fa fede
    /// <see cref="TotpAttivo"/>, non la presenza del segreto.
    /// </summary>
    public string? TotpSecret { get; set; }

    /// <summary>
    /// True solo dopo che l'utente ha digitato un primo codice valido: finché è false il login non
    /// chiede nulla, così chi avvia l'attivazione e poi chiude la pagina non resta chiuso fuori.
    /// </summary>
    public bool TotpAttivo { get; set; }

    public DateTime? TotpAttivatoAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UtenteStruttura> Strutture { get; set; } = new List<UtenteStruttura>();

    public ICollection<CodiceRecuperoUtente> CodiciRecupero { get; set; } = new List<CodiceRecuperoUtente>();

    public ICollection<DispositivoFidato> DispositiviFidati { get; set; } = new List<DispositivoFidato>();
}
