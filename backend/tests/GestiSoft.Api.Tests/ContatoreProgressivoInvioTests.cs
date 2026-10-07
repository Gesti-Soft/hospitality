using GestiSoft.Infrastructure.Persistence;
using GestiSoft.Infrastructure.Repositories;
using GestiSoft.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestiSoft.Api.Tests;

/// <summary>
/// Il progressivo del nome file non deve mai ripetersi per lo stesso codice fiscale, nemmeno con
/// due fatture spedite nello stesso istante: un doppione è uno scarto 00002. Prova su un Postgres vero.
/// </summary>
public class ContatoreProgressivoInvioTests
{
    [FactConPostgres]
    public async Task ChiamateContemporanee_NumeriTuttiDiversi_EOgniCodiceHaIlSuo()
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
                .UseNpgsql(new NpgsqlConnectionStringBuilder(server.ConnectionString) { Database = nomeDatabase }.ConnectionString, npgsql => npgsql.EnableRetryOnFailure())
                .Options;
            var credenziali = new CredenzialiProtector(new byte[32]);
            await using (var db = new GestiSoftDbContext(options, credenziali))
            {
                await db.Database.MigrateAsync();
            }

            async Task<int> Prossimo(string codice)
            {
                await using var db = new GestiSoftDbContext(options, credenziali);
                return await new TrasmissioneFatturaRepository(db).ProssimoProgressivoInvioAsync(codice, 1000, CancellationToken.None);
            }

            var numeri = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Prossimo("ZMMGTN97E08G273L")));
            Assert.Equal(Enumerable.Range(1000, 20), numeri.Order());

            // Un altro codice fiscale parte dal suo valore iniziale, senza toccare il primo.
            Assert.Equal(1000, await Prossimo("01234567890"));
            Assert.Equal(1020, await Prossimo("ZMMGTN97E08G273L"));
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
