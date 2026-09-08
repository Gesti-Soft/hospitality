using GestiSoft.Application.Osservatorio;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Osservatorio;
using Microsoft.Extensions.DependencyInjection;

namespace GestiSoft.Api.Tests;

/// <summary>
/// Verifica il meccanismo di selezione del client Osservatorio per Regione (keyed DI) — l'estensione
/// pensata per collegare altri sistemi regionali oltre a Sicilia (es. ROSS1000) senza toccare
/// OsservatorioInvioService/OsservatorioConfigService. Riproduce solo le due righe di registrazione
/// rilevanti di DependencyInjection.cs, non l'intero AddInfrastructure (che richiede una connection
/// string reale per il DbContext, estranea a questo test).
/// </summary>
public class OsservatorioClientResolverTests
{
    [Fact]
    public void Risolvi_Sicilia_ritorna_il_client_registrato_con_quella_chiave()
    {
        var services = new ServiceCollection();
        services.AddHttpClient<IOsservatorioClient, OsservatorioClient>();
        services.AddKeyedScoped<IOsservatorioClient>(ProviderOsservatorio.Sicilia, (sp, _) => sp.GetRequiredService<IOsservatorioClient>());
        services.AddScoped<IOsservatorioClientResolver, OsservatorioClientResolver>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IOsservatorioClientResolver>();

        var client = resolver.Risolvi(ProviderOsservatorio.Sicilia);

        Assert.IsType<OsservatorioClient>(client);
    }

    [Fact]
    public void Risolvi_provider_senza_implementazione_registrata_lancia_invece_di_restituire_null()
    {
        var services = new ServiceCollection();
        services.AddScoped<IOsservatorioClientResolver, OsservatorioClientResolver>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IOsservatorioClientResolver>();

        Assert.Throws<InvalidOperationException>(() => resolver.Risolvi(ProviderOsservatorio.Sicilia));
    }
}
