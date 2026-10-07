using System.Security.Cryptography;
using System.Text;
using GestiSoft.Application;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Fatturazione.Sdi;
using GestiSoft.Infrastructure;
using GestiSoft.Infrastructure.Fatturazione;
using GestiSoft.Sdi;
using Microsoft.Extensions.Options;
using Serilog;

// Il servizio del canale SdI: l'unico processo che vede i certificati del canale. Spedisce le fatture
// (su richiesta dell'Api), riceve le ricevute e le fatture dei fornitori (dallo SdI, attraverso il
// vhost Apache con mutua autenticazione) e aggiorna lo stato delle fatture.
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddSingleton<ISdiTrasmissioneClient, SdiTrasmissioneClient>();
builder.Services.AddScoped<SdiInvioService>();
builder.Services.AddScoped<SdiRicevuteService>();
builder.Services.AddScoped<SdiRicezioneService>();

var app = builder.Build();
var opzioni = app.Services.GetRequiredService<IOptions<SdiOptions>>().Value;
var log = app.Services.GetRequiredService<ILogger<Program>>();

if (string.IsNullOrWhiteSpace(opzioni.ClientVerifiedHeader))
{
    log.LogWarning("Sdi:ClientVerifiedHeader vuoto: il controllo del certificato dello SdI è SPENTO. Ammesso solo in sviluppo.");
}

// Log di accesso dei servizi SdI: quando qualcosa non va, il portale dice solo "Err: NULL". Senza
// questa riga, distinguere "non è arrivata" da "è arrivata e ho risposto male" richiede un pomeriggio.
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/sdi"))
    {
        await next();
        return;
    }

    var corpoOriginale = context.Response.Body;
    using var risposta = new MemoryStream();
    context.Response.Body = risposta;
    try
    {
        await next();
    }
    finally
    {
        risposta.Position = 0;
        var estratto = Encoding.UTF8.GetString(risposta.ToArray().AsSpan(0, (int)Math.Min(risposta.Length, 300)));
        log.LogInformation(
            "SdI {Metodo} {Percorso} in={ByteIn} stato={Stato} out={ByteOut} risposta={Estratto}",
            context.Request.Method, context.Request.Path, context.Request.ContentLength, context.Response.StatusCode, risposta.Length, estratto);
        risposta.Position = 0;
        await risposta.CopyToAsync(corpoOriginale);
        context.Response.Body = corpoOriginale;
    }
});

// Prova che Apache ha verificato il certificato client dello SdI (vhost con SSLVerifyClient e il CN
// esatto dello SdI). Senza, chiunque raggiungesse il servizio potrebbe dichiararsi lo SdI. Per le
// fatture in arrivo è anche ciò che decide se dare la presa in carico: vedi RicezioneFatture.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/sdi") && !context.Request.Path.StartsWithSegments("/sdi/stato") && !Verificato(context, opzioni))
    {
        log.LogWarning("Chiamata a {Percorso} senza certificato client verificato: rifiutata.", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    await next();
});

app.MapGet("/health", () => Results.Text("ok"));

// Sonda della sorveglianza: risponde solo se la verifica del certificato è arrivata fin qui.
app.MapMethods("/sdi/stato", ["GET", "POST"], (HttpContext context) =>
    Results.Text(Verificato(context, opzioni) ? "OK" : "NON VERIFICATO"));

// Ricevute sulle fatture spedite. Sei operazioni one-way, nessuna risposta SOAP: si risponde con
// l'HTTP e basta. Mai un Fault, mai un 500: lo SdI lo leggerebbe come "riprova" all'infinito.
app.MapPost("/sdi/TrasmissioneFatture", (HttpContext c, SdiRicevuteService s, CancellationToken ct) => RicevutaAsync(c, s, prova: false, ct));
app.MapPost("/sdi/test/TrasmissioneFatture", (HttpContext c, SdiRicevuteService s, CancellationToken ct) => RicevutaAsync(c, s, prova: true, ct));

// Fatture dei fornitori in arrivo.
app.MapPost("/sdi/RicezioneFatture", (HttpContext c, SdiRicezioneService s, CancellationToken ct) => RicezioneAsync(c, s, prova: false, ct));
app.MapPost("/sdi/test/RicezioneFatture", (HttpContext c, SdiRicezioneService s, CancellationToken ct) => RicezioneAsync(c, s, prova: true, ct));

// Chiamate interne dall'Api, sulla rete dei container: Apache non le inoltra (passa solo /sdi/), e
// in più vogliono il segreto condiviso.
app.MapPost("/interno/fatture/invia", async (HttpContext context, RichiestaInvioSdi richiesta, SdiInvioService invio, CancellationToken ct) =>
{
    if (!ChiaveInternaValida(context, opzioni))
    {
        return Results.NotFound();
    }

    try
    {
        return Results.Ok(await invio.InviaAsync(richiesta.StrutturaId, richiesta.FatturaId, richiesta.Operatore, ct));
    }
    catch (DomainException ex)
    {
        return Results.Problem(detail: ex.UserMessage, statusCode: ex.StatusCode);
    }
});

app.Run();

async Task<IResult> RicevutaAsync(HttpContext context, SdiRicevuteService ricevute, bool prova, CancellationToken ct)
{
    try
    {
        var chiamata = ServizioSoap.Leggi(await LeggiCorpoAsync(context, ct));
        if (chiamata is null)
        {
            log.LogWarning("Ricevuta SdI senza corpo leggibile.");
        }
        else
        {
            log.LogInformation("{Prova}{Operazione}: identificativo {Id}, file {File}", prova ? "[COLLAUDO] " : "", chiamata.Operazione.Name.LocalName, chiamata.IdentificativoSdi, chiamata.NomeFile);
            if (chiamata.File is { Length: > 0 } file)
            {
                await ricevute.ImportaAsync(chiamata.NomeFile, chiamata.IdentificativoSdi, file, prova, ct);
            }
        }
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Ricevuta SdI non elaborata.");
    }

    return Results.StatusCode(StatusCodes.Status202Accepted);
}

async Task<IResult> RicezioneAsync(HttpContext context, SdiRicezioneService ricezione, bool prova, CancellationToken ct)
{
    var chiamata = ServizioSoap.Leggi(await LeggiCorpoAsync(context, ct));
    if (chiamata is null)
    {
        log.LogWarning("Chiamata di ricezione senza corpo leggibile.");
        return Results.BadRequest();
    }

    log.LogInformation("{Prova}{Operazione} (namespace {Ns}): identificativo {Id}, file {File}",
        prova ? "[COLLAUDO] " : "", chiamata.Operazione.Name.LocalName, chiamata.Operazione.Name.NamespaceName, chiamata.IdentificativoSdi, chiamata.NomeFile);

    // Decorrenza termini: solo fatture verso la PA. Si accetta in silenzio, senza risposta né Fault.
    if (chiamata.Operazione.Name.LocalName != "fileSdIConMetadati")
    {
        return Results.StatusCode(StatusCodes.Status202Accepted);
    }

    if (chiamata.IdentificativoSdi is null || chiamata.NomeFile is null || chiamata.File is not { Length: > 0 } file)
    {
        log.LogWarning("Fattura ricevuta senza identificativo, nome o contenuto: nessuna presa in carico.");
        return Results.BadRequest();
    }

    var metadati = ServizioSoap.Base64(LettoreXmlSdi.Figlio(chiamata.Operazione, "Metadati"));
    var alSicuro = await ricezione.RiceviAsync(
        chiamata.IdentificativoSdi, chiamata.NomeFile, file, LettoreXmlSdi.Figlio(chiamata.Operazione, "NomeFileMetadati"), metadati, prova, ct);

    // Senza copia sicura niente ER01: lo SdI ritenta, e il guasto si vede invece di perdere la fattura.
    return alSicuro
        ? Results.Text(ServizioSoap.RispostaEr01(chiamata.Operazione), "text/xml; charset=utf-8")
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}

static async Task<byte[]> LeggiCorpoAsync(HttpContext context, CancellationToken ct)
{
    using var corpo = new MemoryStream();
    await context.Request.Body.CopyToAsync(corpo, ct);
    return corpo.ToArray();
}

static bool Verificato(HttpContext context, SdiOptions opzioni) =>
    string.IsNullOrWhiteSpace(opzioni.ClientVerifiedHeader)
    || string.Equals(context.Request.Headers[opzioni.ClientVerifiedHeader].ToString(), "SUCCESS", StringComparison.Ordinal);

static bool ChiaveInternaValida(HttpContext context, SdiOptions opzioni)
{
    var attesa = opzioni.ChiaveInterna;
    var ricevuta = context.Request.Headers[ServizioSdiHttp.IntestazioneChiave].ToString();
    return !string.IsNullOrWhiteSpace(attesa) && attesa.Length >= 24
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(attesa), Encoding.UTF8.GetBytes(ricevuta));
}

public partial class Program;
