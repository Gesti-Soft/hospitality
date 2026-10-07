using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Fatturazione.Sdi;
using Microsoft.Extensions.Options;

namespace GestiSoft.Infrastructure.Fatturazione;

/// <summary>
/// Chiamata dall'Api al servizio SdI sulla rete interna dei container, con il segreto condiviso.
/// Il servizio risponde con l'esito o con un ProblemDetails il cui "detail" è già il messaggio per
/// l'operatore: si gira così com'è.
/// </summary>
public class ServizioSdiHttp(HttpClient http, IOptions<SdiOptions> options) : IServizioSdi
{
    public const string IntestazioneChiave = "X-Sdi-Chiave";

    public async Task<EsitoInvioSdi> InviaFatturaAsync(Guid strutturaId, Guid fatturaId, string? operatore, CancellationToken cancellationToken)
    {
        var opzioni = options.Value;
        if (string.IsNullOrWhiteSpace(opzioni.IndirizzoServizio) || string.IsNullOrWhiteSpace(opzioni.ChiaveInterna))
        {
            throw new ConflictException("Il servizio di invio allo SdI non è configurato. Contatta l'assistenza.");
        }

        using var richiesta = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(opzioni.IndirizzoServizio), "/interno/fatture/invia"))
        {
            Content = JsonContent.Create(new RichiestaInvioSdi(strutturaId, fatturaId, operatore)),
        };
        richiesta.Headers.Add(IntestazioneChiave, opzioni.ChiaveInterna);

        HttpResponseMessage risposta;
        try
        {
            risposta = await http.SendAsync(richiesta, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Il servizio può aver spedito il file prima di cadere: la fattura dice com'è andata.
            throw new ConflictException("Il servizio di invio allo SdI non ha risposto. Ricarica la pagina e controlla lo stato della fattura prima di riprovare.");
        }

        using (risposta)
        {
            if (risposta.IsSuccessStatusCode)
            {
                return await risposta.Content.ReadFromJsonAsync<EsitoInvioSdi>(cancellationToken)
                    ?? throw new ConflictException("Risposta del servizio SdI non valida.");
            }

            var dettaglio = await Dettaglio(risposta, cancellationToken);
            throw risposta.StatusCode switch
            {
                HttpStatusCode.Forbidden => new ForbiddenException(dettaglio ?? "Invio allo SdI non consentito."),
                HttpStatusCode.NotFound => new NotFoundException(dettaglio ?? "Fattura non trovata."),
                HttpStatusCode.Conflict => new ConflictException(dettaglio ?? "Invio allo SdI non riuscito."),
                _ => new ConflictException("Il servizio di invio allo SdI ha avuto un problema. Ricarica la pagina e controlla lo stato della fattura prima di riprovare."),
            };
        }
    }

    private static async Task<string?> Dettaglio(HttpResponseMessage risposta, CancellationToken cancellationToken)
    {
        try
        {
            using var json = await JsonDocument.ParseAsync(await risposta.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return json.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
