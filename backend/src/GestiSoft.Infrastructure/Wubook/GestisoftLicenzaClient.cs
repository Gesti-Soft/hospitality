using System.Net.Http.Json;
using System.Text.Json;
using GestiSoft.Application.Wubook;

namespace GestiSoft.Infrastructure.Wubook;

/// <summary>
/// Client verso il backend esterno "gestisoft" (GestiSoftWeb/UserService, già in produzione).
/// HttpClient tipizzato con BaseAddress configurata (vedi DependencyInjection) — il legacy usava
/// una env var letta a runtime chiamata letteralmente "gestisoft"; qui la stessa cosa passa da
/// configurazione standard .NET (Gestisoft:BaseUrl / env var Gestisoft__BaseUrl).
/// Ogni fallimento di trasporto (URL non configurato, gestisoft.it irraggiungibile, timeout) viene
/// intercettato qui e tradotto in uno status "errore" invece di propagare l'eccezione: chi chiama
/// (WubookLicenzaService) lo gestisce già come un normale rifiuto di licenza, così un problema di
/// configurazione non finisce mai in un 500 grezzo all'utente (principio stabilito in Fase 2).
/// </summary>
public class GestisoftLicenzaClient(HttpClient http) : IGestisoftLicenzaClient
{
    public async Task<LicenzaRisultato> SetRunningAsync(string username, string token, bool? isRunning, CancellationToken cancellationToken)
    {
        var body = await PostAsync("gst-admin/users/set-running", new { username, token, isRunning }, cancellationToken);
        if (body is null)
        {
            return new LicenzaRisultato("errore", "Backend gestisoft.it non raggiungibile.", null, null, null, false);
        }

        var status = body.Value.TryGetProperty("status", out var statusEl) ? statusEl.GetString() ?? "errore" : "errore";
        var messaggio = body.Value.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : body.Value.TryGetProperty("error", out var errEl) ? errEl.GetString() : null;
        var tokenWb = body.Value.TryGetProperty("tokenWb", out var twEl) ? twEl.GetString() : null;
        var idWoBook = body.Value.TryGetProperty("idWoBook", out var iwEl) ? iwEl.GetString() : null;
        var idPaytourist = body.Value.TryGetProperty("idPaytourist", out var ipEl) ? ipEl.GetString() : null;
        var isRunningRisposta = body.Value.TryGetProperty("isRunning", out var irEl) && irEl.ValueKind == JsonValueKind.True;

        return new LicenzaRisultato(status, messaggio, tokenWb, idWoBook, idPaytourist, isRunningRisposta);
    }

    public async Task<EventiNonLettiRisultato> GetEventiNonLettiAsync(string token, CancellationToken cancellationToken)
    {
        var body = await PostAsync("gst-admin/wubook/events/unread-by-token", new { token }, cancellationToken);
        if (body is null)
        {
            return new EventiNonLettiRisultato("errore", Array.Empty<string>());
        }

        var status = body.Value.TryGetProperty("status", out var statusEl) ? statusEl.GetString() ?? "errore" : "errore";
        var rcodes = body.Value.TryGetProperty("rcodes", out var rcodesEl) && rcodesEl.ValueKind == JsonValueKind.Array
            ? rcodesEl.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(s => s.Length > 0).ToList()
            : new List<string>();

        return new EventiNonLettiRisultato(status, rcodes);
    }

    public async Task MarkReadAsync(string token, IReadOnlyList<string> rcodes, CancellationToken cancellationToken)
    {
        // Fallimento qui non è critico: le prenotazioni già importate restano tali, gli rcode
        // semplicemente non vengono marcati "letti" e verranno rielaborati (idempotente, dedup su
        // IdPrenotazioneWubook) al prossimo giro del polling minute-by-minute.
        await PostAsync("gst-admin/wubook/events/mark-read-by-token", new { token, rCodes = rcodes }, cancellationToken);
    }

    private async Task<JsonElement?> PostAsync(string path, object payload, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(path, payload, cancellationToken);
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }
}
