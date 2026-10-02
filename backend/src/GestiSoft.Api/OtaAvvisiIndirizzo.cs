using GestiSoft.Application.Exceptions;

namespace GestiSoft.Api;

/// <summary>
/// Indirizzo pubblico a cui l'OTA manda gli avvisi delle prenotazioni:
/// {Frontend:Origin}/api/ota/avviso/{OtaAvvisi:Segreto} (Apache → nginx del frontend → Api).
/// Il segreto sta nel .env del server: senza, l'endpoint non risponde e l'attivazione è rifiutata.
/// </summary>
public static class OtaAvvisiIndirizzo
{
    public static string? Segreto(IConfiguration configuration) =>
        configuration["OtaAvvisi:Segreto"] is { Length: >= 24 } segreto ? segreto.Trim() : null;

    /// <summary>
    /// Null con il motivo se da qui non si può registrare l'indirizzo. Lo sviluppo usa le stesse
    /// credenziali OTA della produzione: attivare (o disattivare) da un indirizzo locale sposterebbe
    /// gli avvisi della struttura vera su un indirizzo che l'OTA non raggiunge.
    /// </summary>
    public static (string? Url, string? Motivo) Costruisci(IConfiguration configuration)
    {
        var segreto = Segreto(configuration);
        if (segreto is null)
        {
            return (null, "Manca OTA_AVVISI_SEGRETO (almeno 24 caratteri) nel .env del server.");
        }

        var origine = configuration["Frontend:Origin"]?.Trim().TrimEnd('/');
        if (!Uri.TryCreate(origine, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback)
        {
            return (null, "Si attiva solo dal gestionale di produzione (indirizzo https pubblico): da qui sposterebbe gli avvisi della struttura vera.");
        }

        return ($"{origine}/api/ota/avviso/{segreto}", null);
    }

    public static string CostruisciORifiuta(IConfiguration configuration)
    {
        var (url, motivo) = Costruisci(configuration);
        return url ?? throw new ConflictException(motivo!);
    }
}
