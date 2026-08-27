using System.Text;
using System.Xml.Linq;
using GestiSoft.Application.AlloggiatiWeb;

namespace GestiSoft.Infrastructure.AlloggiatiWeb;

/// <summary>
/// Client SOAP 1.1 verso il servizio "Alloggiati Web" (GenerateToken/Send) — porta
/// StatePoliceApiRepository del legacy, envelope scritto a mano con System.Xml.Linq invece delle
/// stringhe interpolate + System.Security.SecurityElement.Escape del legacy (stesso risultato, più
/// robusto per caratteri speciali nei valori). BaseAddress configurata via DI (AlloggiatiWeb:Endpoint).
/// </summary>
public class AlloggiatiWebSoapClient(HttpClient http) : IAlloggiatiWebClient
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Svc = "AlloggiatiService";

    public async Task<AlloggiatiWebTokenRisultato> GenerateTokenAsync(string utente, string password, string wsKey, CancellationToken cancellationToken)
    {
        var body = new XElement(Svc + "GenerateToken",
            new XElement(Svc + "Utente", utente),
            new XElement(Svc + "Password", password),
            new XElement(Svc + "WsKey", wsKey));

        var risposta = await InviaAsync(body, "AlloggiatiService/GenerateToken", cancellationToken);
        if (risposta is null)
        {
            return new AlloggiatiWebTokenRisultato(false, null, "Servizio Alloggiati Web non raggiungibile.");
        }

        var token = ValoreDiscendente(risposta, "token");
        if (string.IsNullOrWhiteSpace(token))
        {
            var esito = ValoreDiscendente(risposta, "esito");
            var erroreDes = ValoreDiscendente(risposta, "ErroreDes");
            return new AlloggiatiWebTokenRisultato(false, null, erroreDes ?? $"Token non ottenuto (esito: {esito ?? "sconosciuto"}).");
        }

        return new AlloggiatiWebTokenRisultato(true, token, null);
    }

    public async Task<AlloggiatiWebInvioRisultato> SendAsync(string utente, string token, IReadOnlyList<string> schedine, CancellationToken cancellationToken)
    {
        var body = new XElement(Svc + "Send",
            new XElement(Svc + "Utente", utente),
            new XElement(Svc + "token", token),
            new XElement(Svc + "ElencoSchedine", schedine.Select(s => new XElement(Svc + "string", s))));

        var risposta = await InviaAsync(body, "AlloggiatiService/Send", cancellationToken);
        if (risposta is null)
        {
            return new AlloggiatiWebInvioRisultato(false, null, "Servizio Alloggiati Web non raggiungibile.", null);
        }

        var sendResult = risposta.Descendants().FirstOrDefault(e => e.Name.LocalName == "SendResult");
        var esito = FiglioDiretto(sendResult, "esito")?.Value;

        if (esito == "false")
        {
            return new AlloggiatiWebInvioRisultato(
                false,
                FiglioDiretto(sendResult, "ErroreCod")?.Value,
                FiglioDiretto(sendResult, "ErroreDes")?.Value ?? "Invio rifiutato dal servizio Alloggiati Web.",
                FiglioDiretto(sendResult, "ErroreDettaglio")?.Value);
        }

        // esito == "true": la richiesta è stata accettata nel complesso. Ogni schedina inviata ha
        // comunque un proprio esito puntuale in EsitoOperazioneServizio — a differenza del legacy
        // (che considerava fallito anche un invio riuscito, per via di un placeholder di fallback
        // usato durante il parsing quando quell'elemento non era popolato), qui si segnala un
        // errore solo se viene trovato un ErroreCod realmente valorizzato su una delle operazioni.
        var operazioneConErrore = risposta.Descendants()
            .Where(e => e.Name.LocalName == "EsitoOperazioneServizio")
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(FiglioDiretto(e, "ErroreCod")?.Value));

        if (operazioneConErrore is not null)
        {
            return new AlloggiatiWebInvioRisultato(
                false,
                FiglioDiretto(operazioneConErrore, "ErroreCod")?.Value,
                FiglioDiretto(operazioneConErrore, "ErroreDes")?.Value ?? "Una o più schedine sono state rifiutate.",
                FiglioDiretto(operazioneConErrore, "ErroreDettaglio")?.Value);
        }

        return new AlloggiatiWebInvioRisultato(true, null, null, null);
    }

    private async Task<XDocument?> InviaAsync(XElement bodyContent, string soapAction, CancellationToken cancellationToken)
    {
        var envelope = new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", Soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
            new XElement(Soap + "Body", bodyContent));

        using var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = new StringContent(new XDocument(envelope).ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml"),
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", soapAction);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            var testo = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(testo) ? null : XDocument.Parse(testo);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string? ValoreDiscendente(XDocument documento, string localName) =>
        documento.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static XElement? FiglioDiretto(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
}
