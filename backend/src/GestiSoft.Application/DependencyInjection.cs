using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Clienti;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Finanze;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Application.Strutture;
using GestiSoft.Application.Utenti;
using Microsoft.Extensions.DependencyInjection;

namespace GestiSoft.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra i servizi applicativi (use case, validatori, mapper). Verrà popolato mano a mano
    /// che i moduli di business (camere, prenotazioni, fatturazione, ecc.) vengono portati dal
    /// sistema desktop.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<ILogEventoService, LogEventoService>();
        services.AddScoped<TenantAccessGuard>();
        services.AddScoped<PermessoStrutturaGuard>();
        services.AddScoped<ImpostazioniStrutturaService>();
        services.AddScoped<ClienteService>();
        services.AddScoped<StrutturaService>();
        services.AddScoped<UtenteManagementService>();
        services.AddScoped<CamereService>();
        services.AddScoped<PrezziCameraService>();
        services.AddScoped<CanaliVenditaService>();
        services.AddScoped<PrenotazioniService>();
        services.AddScoped<OspitiService>();
        services.AddScoped<FinanzeService>();
        services.AddScoped<DatiAziendaliService>();
        services.AddScoped<DatiClienteService>();
        services.AddScoped<FatturazioneService>();

        return services;
    }
}
