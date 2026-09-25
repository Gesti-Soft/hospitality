namespace GestiSoft.Infrastructure.Email;

/// <summary>
/// Server SMTP da cui partono le email di servizio (sezione "Email", env var Email__*). Senza Host
/// non parte nulla e lo si scrive nel log: il resto dell'applicazione funziona lo stesso.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string? Host { get; set; }

    /// <summary>587 con STARTTLS. La porta 465 (TLS implicito) non è supportata da System.Net.Mail.</summary>
    public int Port { get; set; } = 587;

    public string? Utente { get; set; }

    public string? Password { get; set; }

    /// <summary>STARTTLS: da lasciare attivo, lo spegne solo un server di posta interno senza certificato.</summary>
    public bool UsaTls { get; set; } = true;

    /// <summary>Indirizzo del mittente. Se vuoto si usa <see cref="Utente"/>.</summary>
    public string? Mittente { get; set; }

    public string NomeMittente { get; set; } = "GestiSoft";

    /// <summary>Casella dell'assistenza GestiSoft: riceve l'avviso di ogni ticket nuovo e di ogni messaggio dei Clienti.</summary>
    public string? DestinatarioAssistenza { get; set; }

    /// <summary>Indirizzo pubblico del gestionale, per i link nelle email. Preso da Frontend:Origin.</summary>
    public string? IndirizzoApplicazione { get; set; }
}
