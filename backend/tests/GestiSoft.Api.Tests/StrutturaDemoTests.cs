using GestiSoft.Application.SuperAdmin;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using GestiSoft.Infrastructure.Repositories;
using GestiSoft.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestiSoft.Api.Tests;

/// <summary>
/// La struttura demo va salvata davvero su Postgres (vincoli, colonne obbligatorie, date UTC) e deve
/// poi poter sparire con l'eliminazione definitiva, come ogni altra struttura.
/// </summary>
public class StrutturaDemoTests
{
    private static readonly CredenzialiProtector Credenziali = new(new byte[32]);

    [FactConPostgres]
    public async Task Si_salva_tutta_e_si_elimina_tutta_lasciando_il_cliente_vero() => await ConDatabaseTemporaneoAsync(async options =>
    {
        var clienteEsistente = new Cliente { RagioneSociale = "Cliente a cui mostrare la demo" };
        var dati = StrutturaDemoGenerator.Genera(DateTime.Today, 20260924, clienteEsistente.Id);

        await using (var db = new GestiSoftDbContext(options, Credenziali))
        {
            db.Clienti.Add(clienteEsistente);
            // Una struttura vera creata un anno fa: l'eliminazione automatica non deve mai toccarla.
            db.Strutture.Add(new Struttura { ClienteId = clienteEsistente.Id, Nome = "Struttura vera", CreatedAtUtc = DateTime.UtcNow.AddYears(-1) });
            await db.SaveChangesAsync();
            await new SuperAdminRepository(db).AggiungiStrutturaDemoAsync(dati, null, CancellationToken.None);
        }

        await using (var db = new GestiSoftDbContext(options, Credenziali))
        {
            var s = dati.Struttura.Id;
            Assert.Equal(1, await db.Clienti.CountAsync());
            Assert.True(await db.Strutture.AnyAsync(x => x.Id == s && x.ClienteId == clienteEsistente.Id && x.Demo));
            Assert.Equal(20, await db.Camere.CountAsync(x => x.StrutturaId == s));
            Assert.Equal(dati.Prenotazioni.Count, await db.Prenotazioni.CountAsync(x => x.StrutturaId == s));
            Assert.Equal(dati.OspitiRighe.Count, await db.OspitiRighe.CountAsync(x => x.StrutturaId == s));
            Assert.Equal(dati.Pagamenti.Sum(p => p.Importo), await db.PagamentiPrenotazione.Where(x => x.StrutturaId == s).SumAsync(x => x.Importo));

            var repository = new SuperAdminRepository(db);
            Assert.Empty(await repository.ListaDemoScaduteAsync(DateTime.UtcNow.AddDays(-SuperAdminService.GiorniDemo), CancellationToken.None));
            Assert.Equal([s], await repository.ListaDemoScaduteAsync(DateTime.UtcNow.AddMinutes(1), CancellationToken.None));

            await repository.EliminaStrutturaAsync(s, CancellationToken.None);
        }

        await using (var db = new GestiSoftDbContext(options, Credenziali))
        {
            Assert.Equal(["Struttura vera"], await db.Strutture.Select(x => x.Nome).ToListAsync());
            Assert.True(await db.Clienti.AnyAsync(x => x.Id == clienteEsistente.Id));
            Assert.Equal(0, await db.Prenotazioni.CountAsync());
            Assert.Equal(0, await db.Ospiti.CountAsync());
        }
    });

    [FactConPostgres]
    public async Task Eliminata_la_demo_sparisce_anche_il_cliente_demo_con_il_suo_accesso() => await ConDatabaseTemporaneoAsync(async options =>
    {
        var dati = StrutturaDemoGenerator.Genera(DateTime.Today, 1, clienteId: null, nomeClienteNuovo: "Hotel Rossi demo");
        var titolare = new Utente { Email = "demo@prova.invalid", ClienteId = dati.Cliente!.Id, IsClienteAccount = true, PasswordHash = "x" };

        await using (var db = new GestiSoftDbContext(options, Credenziali))
        {
            await new SuperAdminRepository(db).AggiungiStrutturaDemoAsync(dati, titolare, CancellationToken.None);
        }

        await using (var db = new GestiSoftDbContext(options, Credenziali))
        {
            Assert.True(await db.Clienti.AnyAsync(x => x.Id == dati.Cliente.Id && x.Demo && x.RagioneSociale == "Hotel Rossi demo"));
            Assert.True(await db.Utenti.AnyAsync(x => x.Id == titolare.Id));

            await new SuperAdminRepository(db).EliminaStrutturaAsync(dati.Struttura.Id, CancellationToken.None);
        }

        await using (var db = new GestiSoftDbContext(options, Credenziali))
        {
            Assert.Equal(0, await db.Clienti.CountAsync());
            Assert.Equal(0, await db.Utenti.CountAsync());
            Assert.Equal(0, await db.Strutture.CountAsync());
        }
    });

    private static async Task ConDatabaseTemporaneoAsync(Func<DbContextOptions<GestiSoftDbContext>, Task> prova)
    {
        var server = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(FactConPostgresAttribute.VariabileAmbiente));
        var nomeDatabase = $"gestisoft_test_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(server.ConnectionString))
        {
            await admin.OpenAsync();
            await using var crea = new NpgsqlCommand($"CREATE DATABASE \"{nomeDatabase}\"", admin);
            await crea.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<GestiSoftDbContext>()
                // Stessa strategia di retry dell'Api: senza, una transazione aperta nel modo sbagliato passa qui e fallisce in produzione.
                .UseNpgsql(new NpgsqlConnectionStringBuilder(server.ConnectionString) { Database = nomeDatabase }.ConnectionString, npgsql => npgsql.EnableRetryOnFailure())
                .Options;

            await using (var db = new GestiSoftDbContext(options, Credenziali))
            {
                await db.Database.MigrateAsync();
            }

            await prova(options);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var admin = new NpgsqlConnection(server.ConnectionString);
            await admin.OpenAsync();
            await using var elimina = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{nomeDatabase}\" WITH (FORCE)", admin);
            await elimina.ExecuteNonQueryAsync();
        }
    }
}
