using GestiSoft.Application.Fatturazione;
using Microsoft.Extensions.Options;

namespace GestiSoft.Infrastructure.Fatturazione;

/// <summary>
/// Configurazione del canale SdI (sezione "Sdi", env var Sdi__*). Sono dati del canale GestiSoft,
/// come i certificati: valgono per tutte le strutture che spediscono da qui.
/// </summary>
public class SdiOptions
{
    public const string SectionName = "Sdi";

    /// <summary>Codice fiscale del titolare del canale (IdTrasmittente e prefisso del nome file). Non la partita IVA.</summary>
    public string? TrasmittenteCodiceFiscale { get; set; }

    /// <summary>Primo progressivo di un codice fiscale che non ne ha ancora uno: 1000 per stare sopra quelli spesi fuori.</summary>
    public int ProgressivoIniziale { get; set; } = 1000;

    /// <summary>
    /// Servizio RiceviFile dello SdI. È l'unico interruttore fra collaudo e produzione: finché contiene
    /// "testservizi." le fatture spedite non risultano inviate.
    /// </summary>
    public string Endpoint { get; set; } = "https://testservizi.fatturapa.it/ricevi_file";

    /// <summary>Certificato client del canale con la sua chiave (.p12). Lo legge solo il servizio SdI.</summary>
    public string? KeystorePath { get; set; }

    public string? KeystorePassword { get; set; }

    /// <summary>CA dell'Agenzia (produzione e collaudo) in un PEM: per il server di collaudo, firmato da loro.</summary>
    public string? TruststorePath { get; set; }

    /// <summary>
    /// Intestazione con cui Apache dice al servizio che il certificato client dello SdI è stato
    /// verificato. Vuota solo in sviluppo, dove davanti non c'è la mutua autenticazione.
    /// </summary>
    public string? ClientVerifiedHeader { get; set; } = "X-SdI-Client-Verified";

    /// <summary>Cartella dell'archivio su disco delle fatture ricevute (volume incluso nei backup).</summary>
    public string ArchivioPath { get; set; } = "/var/lib/gestisoft/sdi-ricevuti";

    /// <summary>Indirizzo del servizio SdI sulla rete interna, per l'Api (es. http://sdi:8080).</summary>
    public string? IndirizzoServizio { get; set; }

    /// <summary>Segreto condiviso fra Api e servizio SdI per le chiamate interne. Almeno 24 caratteri casuali.</summary>
    public string? ChiaveInterna { get; set; }
}

public class ConfigurazioneCanaleSdi(IOptions<SdiOptions> options) : IConfigurazioneCanaleSdi
{
    public string? CodiceFiscaleTrasmittente =>
        string.IsNullOrWhiteSpace(options.Value.TrasmittenteCodiceFiscale) ? null : options.Value.TrasmittenteCodiceFiscale.Trim().ToUpperInvariant();

    public int ProgressivoIniziale => Math.Max(1, options.Value.ProgressivoIniziale);
}
