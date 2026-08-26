using GestiSoft.Infrastructure.Persistence;
using GestiSoft.Infrastructure.Seed;
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

        return services;
    }
}
