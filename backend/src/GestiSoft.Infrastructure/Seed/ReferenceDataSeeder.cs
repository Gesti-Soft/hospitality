using System.Reflection;
using GestiSoft.Domain.Entities.Riferimenti;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestiSoft.Infrastructure.Seed;

/// <summary>
/// Popola le tabelle di riferimento condivise (Comuni/Stati/Documenti d'identità/TipoAlloggiato)
/// usate dalle schede "Alloggiati Web". Sostituisce la logica legacy
/// (OrderManagement.Logic.StartUpLogic.TablePopulationControl) che leggeva gli stessi dati da un
/// path assoluto Windows (`C:\GestiSoft\Hospitality\OrderManagement\...txt`, UTF-16LE): qui i dati
/// sono risorse embedded nell'assembly (CSV UTF-8), quindi non serve alcun file esterno a runtime.
/// Idempotente: ogni tabella viene popolata solo se vuota, come nel sistema legacy.
/// </summary>
public class ReferenceDataSeeder(GestiSoftDbContext db, ILogger<ReferenceDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedComuniAsync(cancellationToken);
        await SeedStatiAsync(cancellationToken);
        await SeedDocumentiAsync(cancellationToken);
        await SeedTipiAlloggiatoAsync(cancellationToken);
    }

    private async Task SeedComuniAsync(CancellationToken ct)
    {
        if (await db.Comuni.AnyAsync(ct))
        {
            return;
        }

        var righe = ReadEmbeddedCsvLines("comuni.csv");
        var comuni = new List<Comune>(righe.Count);

        foreach (var colonne in righe)
        {
            comuni.Add(new Comune
            {
                Codice = long.TryParse(colonne[1].Trim(), out var codice) ? codice : 0,
                Descrizione = colonne[2].Trim(),
                Provincia = colonne[3].Trim(),
                CodiceBelfiore = colonne[5].Trim(),
                Cap = colonne[6].Trim(),
            });
        }

        db.Comuni.AddRange(comuni);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed Comuni: inserite {Count} righe", comuni.Count);
    }

    private async Task SeedStatiAsync(CancellationToken ct)
    {
        if (await db.Stati.AnyAsync(ct))
        {
            return;
        }

        var righe = ReadEmbeddedCsvLines("stati.csv");
        var stati = new List<Stato>(righe.Count);

        foreach (var colonne in righe)
        {
            stati.Add(new Stato
            {
                Codice = long.TryParse(colonne[1].Trim(), out var codice) ? codice : 0,
                Descrizione = colonne[2].Trim(),
                NomeInglese = colonne[3].Trim(),
                Acronimo = colonne[5].Trim(),
            });
        }

        db.Stati.AddRange(stati);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed Stati: inserite {Count} righe", stati.Count);
    }

    private async Task SeedDocumentiAsync(CancellationToken ct)
    {
        if (await db.DocumentiIdentita.AnyAsync(ct))
        {
            return;
        }

        var righe = ReadEmbeddedCsvLines("documenti.csv");
        var documenti = new List<Documento>(righe.Count);

        foreach (var colonne in righe)
        {
            documenti.Add(new Documento
            {
                Codice = colonne[1].Trim(),
                Descrizione = colonne[2].Trim(),
                TypeId = int.Parse(colonne[3].Trim()),
            });
        }

        db.DocumentiIdentita.AddRange(documenti);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed Documenti identità: inserite {Count} righe", documenti.Count);
    }

    private async Task SeedTipiAlloggiatoAsync(CancellationToken ct)
    {
        if (await db.TipiAlloggiato.AnyAsync(ct))
        {
            return;
        }

        var righe = ReadEmbeddedCsvLines("tipo_alloggiato.csv");
        var tipi = new List<TipoAlloggiato>(righe.Count);

        foreach (var colonne in righe)
        {
            tipi.Add(new TipoAlloggiato
            {
                Codice = colonne[1].Trim(),
                Descrizione = colonne[2].Trim(),
            });
        }

        db.TipiAlloggiato.AddRange(tipi);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed TipoAlloggiato: inserite {Count} righe", tipi.Count);
    }

    private static List<string[]> ReadEmbeddedCsvLines(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"GestiSoft.Infrastructure.Seed.Data.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Risorsa embedded non trovata: {resourceName}");
        using var reader = new StreamReader(stream);

        var righe = new List<string[]>();
        string? riga;
        while ((riga = reader.ReadLine()) is not null)
        {
            if (riga.Length == 0)
            {
                continue;
            }

            righe.Add(riga.Split(','));
        }

        return righe;
    }
}
