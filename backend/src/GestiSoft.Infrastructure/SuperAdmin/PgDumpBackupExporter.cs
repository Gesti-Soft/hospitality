using System.Diagnostics;
using GestiSoft.Application.SuperAdmin;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GestiSoft.Infrastructure.SuperAdmin;

/// <summary>
/// Lancia <c>pg_dump</c> come processo esterno verso lo stesso Postgres già usato da EF Core (via
/// rete, stessa connection string) — richiede il client <c>postgresql-client-17</c> installato
/// nell'immagine runtime dell'Api (vedi Dockerfile). La password non passa mai sulla riga di
/// comando (visibile in una eventuale process list), solo via variabile d'ambiente PGPASSWORD.
/// </summary>
public class PgDumpBackupExporter(IConfiguration configuration) : IBackupExporter
{
    public async Task<byte[]> EsportaAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' non configurata.");
        var csb = new NpgsqlConnectionStringBuilder(connectionString);

        var startInfo = new ProcessStartInfo
        {
            FileName = "pg_dump",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add(csb.Host ?? "postgres");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(csb.Port.ToString());
        startInfo.ArgumentList.Add("--username");
        startInfo.ArgumentList.Add(csb.Username ?? string.Empty);
        startInfo.ArgumentList.Add("--dbname");
        startInfo.ArgumentList.Add(csb.Database ?? string.Empty);
        startInfo.ArgumentList.Add("--format=custom");
        startInfo.Environment["PGPASSWORD"] = csb.Password ?? string.Empty;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Impossibile avviare pg_dump.");

        var stdout = new MemoryStream();
        var copyStdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdout, cancellationToken);
        var readStderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await copyStdoutTask;
        var stderr = await readStderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"pg_dump ha fallito (exit code {process.ExitCode}): {stderr}");
        }

        return stdout.ToArray();
    }
}
