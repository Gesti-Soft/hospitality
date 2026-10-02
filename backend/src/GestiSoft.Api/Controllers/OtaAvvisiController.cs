using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GestiSoft.Application.Wubook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace GestiSoft.Api.Controllers;

/// <summary>
/// Avvisi delle prenotazioni mandati dall'OTA (push_activation): una POST con lcode e rcode a ogni
/// prenotazione nuova, modificata o cancellata. Si registra e si risponde subito 200, come chiede la
/// documentazione (l'OTA riprova e con troppi errori sospende gli avvisi); scaricare e importare la
/// prenotazione è compito del Worker (vedi WubookAvvisiDirettiService).
///
/// Lettura di lcode/rcode ripresa da WuBookController.Push di gestisoft.it, che riceve questi stessi
/// avvisi da anni: query string, form, JSON o XML-RPC. Senza il segreto giusto nel percorso si
/// risponde 404 come per un indirizzo che non esiste. Un avviso falso non può fare danni: la
/// prenotazione si riscarica sempre dall'OTA con le nostre credenziali.
/// </summary>
[ApiController]
[Route("ota/avviso")]
[AllowAnonymous]
public class OtaAvvisiController(WubookAvvisiDirettiService avvisi, IConfiguration configuration, ILogger<OtaAvvisiController> logger) : ControllerBase
{
    [HttpPost("{segreto}")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Ricevi(string segreto, CancellationToken cancellationToken)
    {
        var atteso = OtaAvvisiIndirizzo.Segreto(configuration);
        if (atteso is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(segreto), Encoding.UTF8.GetBytes(atteso)))
        {
            return NotFound();
        }

        var contentType = Request.ContentType ?? "";
        string corpo;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            corpo = await reader.ReadToEndAsync(cancellationToken);
        }

        var (lcode, rcode) = LeggiCodici(contentType, corpo);

        try
        {
            await avvisi.RegistraAvvisoAsync(lcode, rcode, cancellationToken);
        }
        catch (Exception ex)
        {
            // Si risponde comunque 200: un errore nostro non deve far sospendere gli avvisi all'OTA.
            // Il controllo periodico recupera la prenotazione.
            logger.LogError(ex, "Avviso OTA non registrato (lcode={Lcode}, rcode={Rcode}).", lcode, rcode);
        }

        return contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) || corpo.TrimStart().StartsWith('<')
            ? Content("<methodResponse><params><param><value><string>OK</string></value></param></params></methodResponse>", "text/xml")
            : Content("OK", "text/plain");
    }

    private (string? Lcode, string? Rcode) LeggiCodici(string contentType, string corpo)
    {
        string? lcode = Request.Query["lcode"].FirstOrDefault();
        string? rcode = Request.Query["rcode"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(lcode) || string.IsNullOrWhiteSpace(rcode))
        {
            if (contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                var campi = QueryHelpers.ParseQuery("?" + corpo);
                if (campi.TryGetValue("lcode", out var l)) lcode ??= l.ToString();
                if (campi.TryGetValue("rcode", out var r)) rcode ??= r.ToString();
            }
            else if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase) || corpo.TrimStart().StartsWith('{'))
            {
                try
                {
                    using var documento = JsonDocument.Parse(corpo);
                    if (documento.RootElement.TryGetProperty("lcode", out var l)) lcode ??= l.GetRawText().Trim('"');
                    if (documento.RootElement.TryGetProperty("rcode", out var r)) rcode ??= r.GetRawText().Trim('"');
                }
                catch (JsonException)
                {
                }
            }
            else
            {
                lcode ??= MembroXml(corpo, "lcode");
                rcode ??= MembroXml(corpo, "rcode");
            }
        }

        return (string.IsNullOrWhiteSpace(lcode) ? null : lcode.Trim(), string.IsNullOrWhiteSpace(rcode) ? null : rcode.Trim());
    }

    private static string? MembroXml(string corpo, string nome)
    {
        var trovato = Regex.Match(
            corpo,
            $@"<name>\s*{nome}\s*</name>\s*<value>\s*<(?:int|i4|string)>\s*([^<]+)\s*</(?:int|i4|string)>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline,
            TimeSpan.FromSeconds(1));
        return trovato.Success ? trovato.Groups[1].Value.Trim() : null;
    }
}
