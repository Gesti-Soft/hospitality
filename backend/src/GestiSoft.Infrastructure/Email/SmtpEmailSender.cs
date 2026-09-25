using System.Net;
using System.Net.Mail;
using GestiSoft.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestiSoft.Infrastructure.Email;

/// <summary>
/// Invio con System.Net.Mail, già nel framework: per poche email di servizio in chiaro verso un
/// server che accetta STARTTLS basta, senza aggiungere dipendenze. Nel log non finiscono né gli
/// indirizzi né il testo, solo l'esito.
/// </summary>
public class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    // Chi apre un ticket aspetta la risposta della pagina: un server di posta lento non deve
    // tenerlo fermo più di così. L'email persa si recupera, il ticket è già salvato.
    private const int TimeoutMillisecondi = 15_000;

    public Task<bool> InviaAdAssistenzaAsync(string oggetto, string testo, string? percorsoApplicazione, CancellationToken cancellationToken)
    {
        var destinatario = options.Value.DestinatarioAssistenza;
        if (string.IsNullOrWhiteSpace(destinatario))
        {
            logger.LogWarning("Email all'assistenza non inviata: EMAIL_DESTINATARIO_ASSISTENZA non configurato");
            return Task.FromResult(false);
        }

        return InviaAsync(destinatario, oggetto, testo, percorsoApplicazione, cancellationToken);
    }

    public async Task<bool> InviaAsync(string destinatario, string oggetto, string testo, string? percorsoApplicazione, CancellationToken cancellationToken)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Host))
        {
            logger.LogWarning("Email non inviata: server SMTP non configurato (EMAIL_SMTP_HOST)");
            return false;
        }

        var mittente = string.IsNullOrWhiteSpace(o.Mittente) ? o.Utente : o.Mittente;
        if (string.IsNullOrWhiteSpace(mittente))
        {
            logger.LogWarning("Email non inviata: manca l'indirizzo del mittente (EMAIL_MITTENTE)");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(percorsoApplicazione) && !string.IsNullOrWhiteSpace(o.IndirizzoApplicazione))
        {
            testo += $"\n\nApri nel gestionale: {o.IndirizzoApplicazione.TrimEnd('/')}{percorsoApplicazione}";
        }

        try
        {
            using var messaggio = new MailMessage
            {
                From = new MailAddress(mittente, o.NomeMittente),
                Subject = oggetto,
                Body = testo,
                IsBodyHtml = false,
            };
            messaggio.To.Add(destinatario);

            using var client = new SmtpClient(o.Host, o.Port)
            {
                EnableSsl = o.UsaTls,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = TimeoutMillisecondi,
            };
            if (!string.IsNullOrWhiteSpace(o.Utente))
            {
                client.Credentials = new NetworkCredential(o.Utente, o.Password);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeoutMillisecondi);
            await client.SendMailAsync(messaggio, timeout.Token);
            return true;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException or OperationCanceledException)
        {
            // Il messaggio dell'eccezione può riportare la risposta del server, che spesso contiene
            // l'indirizzo rifiutato: nel log va solo il tipo e il codice SMTP.
            var codice = ex is SmtpException smtp ? smtp.StatusCode.ToString() : "-";
            logger.LogWarning("Invio email non riuscito ({TipoErrore}, codice SMTP {Codice})", ex.GetType().Name, codice);
            return false;
        }
    }
}
