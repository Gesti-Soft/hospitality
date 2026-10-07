using System.Globalization;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Fatturazione.Sdi;
using Microsoft.Extensions.Options;

namespace GestiSoft.Infrastructure.Fatturazione;

/// <summary>
/// Il servizio RiceviFile dello SdI, chiamato con il certificato client del canale (mutua
/// autenticazione TLS). Singleton: l'HttpClient si costruisce una volta sola.
/// <para>
/// Il server di produzione NON è firmato dalla CA dell'Agenzia ma da una CA pubblica (Sectigo),
/// quello di collaudo sì: ci si fida delle CA di sistema OPPURE di quella dell'Agenzia. Non è un
/// allentamento: il nome host si verifica sempre, e la nostra identità la dimostra il certificato
/// client. Fidarsi della sola CA dell'Agenzia fa fallire il primo invio vero.
/// </para>
/// </summary>
public sealed class SdiTrasmissioneClient : ISdiTrasmissioneClient, IDisposable
{
    private const string NsTipi = "http://www.fatturapa.gov.it/sdi/ws/trasmissione/v1.0/types";
    private const string SoapAction = "\"http://www.fatturapa.it/SdIRiceviFile/RiceviFile\"";

    private readonly Uri endpoint;
    private readonly Lazy<HttpClient> http;

    public SdiTrasmissioneClient(IOptions<SdiOptions> options)
    {
        var opzioni = options.Value;
        endpoint = new Uri(opzioni.Endpoint);
        // Pigro: senza certificati configurati il servizio parte lo stesso (ricevute e ricezione
        // funzionano), e l'errore arriva solo a chi prova a spedire, con un messaggio chiaro.
        http = new Lazy<HttpClient>(() => Crea(opzioni));
    }

    public bool AmbienteDiProva => endpoint.Host.StartsWith("testservizi.", StringComparison.OrdinalIgnoreCase);

    private static HttpClient Crea(SdiOptions opzioni)
    {
        if (string.IsNullOrWhiteSpace(opzioni.KeystorePath) || !File.Exists(opzioni.KeystorePath))
        {
            throw new InvalidOperationException("Certificato client del canale SdI non configurato (Sdi:KeystorePath).");
        }

        var client = X509CertificateLoader.LoadPkcs12FromFile(opzioni.KeystorePath, opzioni.KeystorePassword);
        var agenzia = new X509Certificate2Collection();
        if (!string.IsNullOrWhiteSpace(opzioni.TruststorePath) && File.Exists(opzioni.TruststorePath))
        {
            agenzia.ImportFromPemFile(opzioni.TruststorePath);
        }

        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(30),
            SslOptions = new SslClientAuthenticationOptions
            {
                ClientCertificates = [client],
                RemoteCertificateValidationCallback = (_, certificato, _, errori) => ServerFidato(certificato, errori, agenzia),
            },
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
    }

    /// <summary>CA di sistema (produzione: Sectigo) oppure CA dell'Agenzia (collaudo). Il nome host non si negozia.</summary>
    internal static bool ServerFidato(X509Certificate? certificato, SslPolicyErrors errori, X509Certificate2Collection agenzia)
    {
        if (errori == SslPolicyErrors.None)
        {
            return true;
        }

        if (certificato is null || errori != SslPolicyErrors.RemoteCertificateChainErrors || agenzia.Count == 0)
        {
            return false;
        }

        using var catena = new X509Chain();
        catena.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        catena.ChainPolicy.CustomTrustStore.AddRange(agenzia);
        catena.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        using var x509 = new X509Certificate2(certificato);
        return catena.Build(x509);
    }

    public async Task<RispostaRiceviFile> InviaAsync(string nomeFile, byte[] contenuto, CancellationToken cancellationToken)
    {
        var busta = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
              <soapenv:Body>
                <ns:fileSdIAccoglienza xmlns:ns="{NsTipi}">
                  <NomeFile>{System.Security.SecurityElement.Escape(nomeFile)}</NomeFile>
                  <File>{Convert.ToBase64String(contenuto)}</File>
                </ns:fileSdIAccoglienza>
              </soapenv:Body>
            </soapenv:Envelope>
            """;

        using var richiesta = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(busta, Encoding.UTF8, "text/xml"),
        };
        richiesta.Headers.TryAddWithoutValidation("SOAPAction", SoapAction);

        HttpClient client;
        try
        {
            client = http.Value;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Security.Cryptography.CryptographicException or IOException)
        {
            // Certificato mancante o password sbagliata: il file non è partito, nessun esito ignoto.
            return new RispostaRiceviFile(null, null, "Il certificato del canale SdI non è utilizzabile: contatta l'assistenza.");
        }

        HttpResponseMessage risposta;
        try
        {
            risposta = await client.SendAsync(richiesta, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            // Qui non sappiamo se lo SdI abbia ricevuto il file: va detto, non nascosto.
            throw new SdiNonRaggiungibileException($"Nessuna risposta dallo SdI: {ex.Message}", ex);
        }

        using (risposta)
        {
            var corpo = await risposta.Content.ReadAsByteArrayAsync(cancellationToken);
            return Interpreta(corpo, (int)risposta.StatusCode);
        }
    }

    /// <summary>
    /// La risposta arriva in MTOM (multipart XOP): il corpo comincia con un delimitatore "--" e la
    /// busta SOAP sta nella prima parte. Letta come XML unico non si capisce niente: è successo al
    /// primo invio vero di chi ci è passato prima.
    /// </summary>
    internal static RispostaRiceviFile Interpreta(byte[] corpo, int statoHttp)
    {
        var radice = LettoreXmlSdi.Radice(EstraiBusta(corpo))
            ?? throw new SdiNonRaggiungibileException($"Risposta dello SdI non interpretabile (HTTP {statoHttp}).");

        if (LettoreXmlSdi.Trova(radice, "Fault") is { } fault)
        {
            // Un Fault è una risposta: il file non è entrato. Si tratta come un rifiuto, non come esito ignoto.
            return new RispostaRiceviFile(null, null, "Fault: " + (LettoreXmlSdi.Testo(fault, "faultstring") ?? "errore SOAP"));
        }

        var dati = LettoreXmlSdi.Trova(radice, "rispostaSdIRiceviFile") ?? radice;
        var identificativo = LettoreXmlSdi.Testo(dati, "IdentificativoSdI");
        var errore = LettoreXmlSdi.Testo(dati, "Errore");
        if (identificativo is null && errore is null)
        {
            throw new SdiNonRaggiungibileException($"Risposta dello SdI senza identificativo né errore (HTTP {statoHttp}).");
        }

        var data = DateTimeOffset.TryParse(LettoreXmlSdi.Testo(dati, "DataOraRicezione"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : (DateTimeOffset?)null;
        return new RispostaRiceviFile(errore is null ? identificativo : null, data, errore);
    }

    internal static byte[] EstraiBusta(byte[] corpo)
    {
        var testo = Encoding.UTF8.GetString(corpo);
        if (!testo.TrimStart().StartsWith("--", StringComparison.Ordinal))
        {
            return corpo;
        }

        var inizio = testo.IndexOf("<?xml", StringComparison.Ordinal);
        if (inizio < 0)
        {
            var envelope = testo.IndexOf("Envelope", StringComparison.Ordinal);
            inizio = envelope < 0 ? -1 : testo.LastIndexOf('<', envelope);
        }

        if (inizio < 0)
        {
            return corpo;
        }

        var fine = testo.IndexOf("\r\n--", inizio, StringComparison.Ordinal);
        if (fine < 0)
        {
            fine = testo.IndexOf("\n--", inizio, StringComparison.Ordinal);
        }

        return Encoding.UTF8.GetBytes(fine < 0 ? testo[inizio..] : testo[inizio..fine]);
    }

    public void Dispose()
    {
        if (http.IsValueCreated)
        {
            http.Value.Dispose();
        }
    }
}
