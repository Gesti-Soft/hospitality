using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Clienti;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Finanze;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Notifiche;
using GestiSoft.Application.Osservatorio;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.PayTourist;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Application.Riferimenti;
using GestiSoft.Application.Statistiche;
using GestiSoft.Application.SuperAdmin;
using GestiSoft.Application.Utenti;
using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.AlloggiatiWeb;
using GestiSoft.Infrastructure.Auth;
using GestiSoft.Infrastructure.Fatturazione;
using GestiSoft.Infrastructure.Osservatorio;
using GestiSoft.Infrastructure.PayTourist;
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
        services.AddScoped<ISpesaRepository, SpesaRepository>();
        services.AddScoped<IEntrataRepository, EntrataRepository>();
        services.AddScoped<IDatiAziendaliRepository, DatiAziendaliRepository>();
        services.AddScoped<IDatiClienteRepository, DatiClienteRepository>();
        services.AddScoped<IDatiFatturaRepository, DatiFatturaRepository>();
        services.AddScoped<IFatturaDocumentGenerator, FatturaDocumentGenerator>();
        services.AddScoped<IWubookIntegrazioneRepository, WubookIntegrazioneRepository>();
        services.AddScoped<IWubookEventoRicevutoRepository, WubookEventoRicevutoRepository>();
        services.AddScoped<IRinnovoLicenzaRepository, RinnovoLicenzaRepository>();
        services.AddScoped<IChiusuraCameraRepository, ChiusuraCameraRepository>();
        services.AddScoped<IRestrizioneSoggiornoCameraRepository, RestrizioneSoggiornoCameraRepository>();
        services.AddScoped<IAlloggiatiWebIntegrazioneRepository, AlloggiatiWebIntegrazioneRepository>();
        services.AddScoped<IAnagraficaAlloggiatiWebRepository, AnagraficaAlloggiatiWebRepository>();
        services.AddScoped<IOsservatorioAppartamentoRepository, OsservatorioAppartamentoRepository>();
        services.AddScoped<IOsservatorioInvioRepository, OsservatorioInvioRepository>();
        services.AddScoped<IPayTouristIntegrazioneRepository, PayTouristIntegrazioneRepository>();
        services.AddScoped<IPayTouristStrutturaRepository, PayTouristStrutturaRepository>();
        services.AddScoped<IRiferimentiRepository, RiferimentiRepository>();
        services.AddScoped<ISuperAdminRepository, SuperAdminRepository>();
        services.AddScoped<IStatisticheRepository, StatisticheRepository>();
        services.AddScoped<IStatisticheSuperAdminRepository, StatisticheSuperAdminRepository>();
        services.AddScoped<IImpostazioniGlobaliRepository, ImpostazioniGlobaliRepository>();
        services.AddScoped<INotificaRepository, NotificaRepository>();
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

        // Servizio SOAP "Alloggiati Web" della Polizia di Stato (Fase 6): il legacy leggeva
        // l'endpoint da una env var ("EndPointPM") mai hardcoded nel codice; qui passa da
        // configurazione standard (AlloggiatiWeb:Endpoint / env var AlloggiatiWeb__Endpoint), non
        // configurato per default in dev — se mancante, le chiamate falliscono solo quando
        // l'integrazione viene effettivamente usata, non bloccano l'avvio dell'Api/Worker.
        var alloggiatiWebEndpoint = configuration["AlloggiatiWeb:Endpoint"];
        services.AddHttpClient<IAlloggiatiWebClient, AlloggiatiWebSoapClient>(client =>
        {
            if (!string.IsNullOrWhiteSpace(alloggiatiWebEndpoint))
            {
                client.BaseAddress = new Uri(alloggiatiWebEndpoint);
            }
        });

        // Osservatorio Turistico (Fase 7): il legacy leggeva l'endpoint dalla env var "EndPointPMS",
        // mai hardcoded; qui passa da configurazione standard (Osservatorio:BaseUrl / env var
        // Osservatorio__BaseUrl), stesso trattamento delle altre integrazioni esterne.
        var osservatorioBaseUrl = configuration["Osservatorio:BaseUrl"];
        services.AddHttpClient<IOsservatorioClient, OsservatorioClient>(client =>
        {
            if (!string.IsNullOrWhiteSpace(osservatorioBaseUrl))
            {
                client.BaseAddress = new Uri(osservatorioBaseUrl.TrimEnd('/') + "/");
            }
        });

        // Un solo sistema regionale verificato/con clienti per ora (Sicilia). Registrato anche con
        // chiave ProviderOsservatorio così OsservatorioClientResolver può scegliere l'implementazione
        // giusta per Appartamento — aggiungere un'altra Regione (es. ROSS1000, adottato da diverse
        // Regioni) significa registrare qui la sua implementazione con la sua chiave, nessun'altra
        // modifica al modulo Osservatorio.
        services.AddKeyedScoped<IOsservatorioClient>(ProviderOsservatorio.Sicilia, (sp, _) => sp.GetRequiredService<IOsservatorioClient>());
        services.AddScoped<IOsservatorioClientResolver, OsservatorioClientResolver>();

        // PayTourist (Fase 8): a differenza delle altre integrazioni esterne, l'host NON è unico —
        // PayTourist assegna un sottodominio per Comune (es. https://palermo.paytourist.com). Qui
        // "PayTourist:BaseUrl" resta un template con il segnaposto "{comune}" (env var
        // PayTourist__BaseUrl, es. "https://{comune}.paytourist.com"), risolto da PayTouristClient
        // ad ogni chiamata in base al Comune Attività della Struttura — quindi nessun
        // HttpClient.BaseAddress fissato qui in DI, su istruzione esplicita dell'utente.
        services.AddHttpClient<IPayTouristClient, PayTouristClient>();

        return services;
    }
}
