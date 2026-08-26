using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GestiSoft.Infrastructure.Seed;

/// <summary>
/// Crea il primo Super Admin se non esiste ancora nessuno — altrimenti non ci sarebbe modo di
/// accedere al gestionale la prima volta. A differenza del legacy (utente "admin"/"admin"
/// hardcoded in chiaro nel codice), qui email e password iniziali vengono da configurazione
/// (env var in produzione) e la password è hashata.
/// </summary>
public class IdentitySeeder(GestiSoftDbContext db, IPasswordHasher<Utente> passwordHasher, IConfiguration configuration, ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Utenti.AnyAsync(u => u.IsSuperAdmin, cancellationToken))
        {
            return;
        }

        var email = configuration["SuperAdmin:Email"];
        var password = configuration["SuperAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Nessun Super Admin presente e SuperAdmin:Email/SuperAdmin:Password non configurati: " +
                "il seeding del primo utente viene saltato.");
            return;
        }

        var superAdmin = new Utente
        {
            Email = email.Trim().ToLowerInvariant(),
            IsSuperAdmin = true,
            ClienteId = null,
            Attivo = true,
        };
        superAdmin.PasswordHash = passwordHasher.HashPassword(superAdmin, password);

        db.Utenti.Add(superAdmin);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed Super Admin: creato utente {Email}", superAdmin.Email);
    }
}
