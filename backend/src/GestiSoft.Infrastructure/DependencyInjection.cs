using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Clienti;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Finanze;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Application.Utenti;
using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Auth;
using GestiSoft.Infrastructure.Fatturazione;
using GestiSoft.Infrastructure.Persistence;
using GestiSoft.Infrastructure.Repositories;
using GestiSoft.Infrastructure.Seed;
using GestiSoft.Infrastructure.Wubook;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestiSoft.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' non configurata (ConnectionStrings:Default oppure env var ConnectionStrings__Default).");

        services.AddDbContext<GestiSoftDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ReferenceDataSeeder>();
        services.AddScoped<IdentitySeeder>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IUtenteRepository, UtenteRepository>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ILogEventoRepository, LogEventoRepository>();
        services.AddScoped<IStrutturaRepository, StrutturaRepository>();
        services.AddScoped<IImpostazioniStrutturaRepository, ImpostazioniStrutturaRepository>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IUtenteStrutturaRepository, UtenteStrutturaRepository>();
        services.AddScoped<ITipologiaCameraRepository, TipologiaCameraRepository>();
        services.AddScoped<ICameraRepository, CameraRepository>();
        services.AddScoped<IPrezzoCameraRepository, PrezzoCameraRepository>();
        services.AddScoped<ICanaleVenditaRepository, CanaleVenditaRepository>();
        services.AddScoped<IPrenotazioneRepository, PrenotazioneRepository>();
        services.AddScoped<ICauzioneRepository, CauzioneRepository>();
        services.AddScoped<IOspiteRepository, OspiteRepository>();
        services.AddScoped<IDatiAziendaliComuneRepository, DatiAziendaliComuneRepository>();
        services.AddScoped<ISpesaRepository, SpesaRepository>();
        services.AddScoped<IEntrataRepository, EntrataRepository>();
        services.AddScoped<IDatiAziendaliRepository, DatiAziendaliRepository>();
        services.AddScoped<IDatiClienteRepository, DatiClienteRepository>();
        services.AddScoped<IDatiFatturaRepository, DatiFatturaRepository>();
        services.AddScoped<IFatturaDocumentGenerator, FatturaDocumentGenerator>();
        services.AddScoped<IWubookIntegrazioneRepository, WubookIntegrazioneRepository>();
        services.AddSingleton<IPasswordHasher<Utente>, PasswordHasher<Utente>>();

        // Backend esterno "gestisoft" (licenze/abbonamenti, già in produzione): il legacy leggeva
        // l'URL da una env var letta a runtime chiamata letteralmente "gestisoft"; qui passa da
        // configurazione standard (Gestisoft:BaseUrl / env var Gestisoft__BaseUrl). Non configurato
        // per default in dev: se mancante, le chiamate falliscono solo quando l'integrazione Wubook
        // viene effettivamente usata, non bloccano l'avvio dell'Api.
        var gestisoftBaseUrl = configuration["Gestisoft:BaseUrl"];
        services.AddHttpClient<IGestisoftLicenzaClient, GestisoftLicenzaClient>(client =>
        {
            if (!string.IsNullOrWhiteSpace(gestisoftBaseUrl))
            {
                client.BaseAddress = new Uri(gestisoftBaseUrl.TrimEnd('/') + "/");
            }
        });
        services.AddHttpClient<IWubookClient, WubookXmlRpcClient>();

        return services;
    }
}
