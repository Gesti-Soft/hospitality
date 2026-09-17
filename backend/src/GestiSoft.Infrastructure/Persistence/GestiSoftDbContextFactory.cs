using GestiSoft.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GestiSoft.Infrastructure.Persistence;

/// <summary>
/// Usato da `dotnet ef migrations add` per creare il DbContext senza dover avviare l'host Api/Worker.
/// La connection string reale in dev/prod arriva sempre da configurazione/env var tramite DependencyInjection;
/// questo fallback serve solo per gli strumenti da riga di comando.
/// </summary>
public class GestiSoftDbContextFactory : IDesignTimeDbContextFactory<GestiSoftDbContext>
{
    public GestiSoftDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("GESTISOFT_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=gestisoft;Username=gestisoft;Password=gestisoft_dev_only";

        var optionsBuilder = new DbContextOptionsBuilder<GestiSoftDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        // Chiave fittizia: la cifratura delle credenziali è un ValueConverter, non tocca lo schema,
        // e gli strumenti da riga di comando non leggono né scrivono dati veri.
        return new GestiSoftDbContext(optionsBuilder.Options, new CredenzialiProtector(new byte[32]));
    }
}
