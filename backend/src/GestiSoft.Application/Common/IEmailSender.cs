namespace GestiSoft.Application.Common;

/// <summary>
/// Invio di email di servizio. Un invio non riuscito o un server di posta non configurato non
/// devono mai far fallire l'operazione che l'ha generato: l'implementazione registra il problema
/// nel log e restituisce false, non solleva eccezioni.
/// </summary>
public interface IEmailSender
{
    /// <param name="percorsoApplicazione">Percorso della pagina da aprire (es. "/assistenza"), aggiunto in fondo come link completo all'indirizzo del gestionale.</param>
    Task<bool> InviaAsync(string destinatario, string oggetto, string testo, string? percorsoApplicazione, CancellationToken cancellationToken);

    /// <summary>Come <see cref="InviaAsync"/>, alla casella dell'assistenza GestiSoft configurata sul server.</summary>
    Task<bool> InviaAdAssistenzaAsync(string oggetto, string testo, string? percorsoApplicazione, CancellationToken cancellationToken);
}
