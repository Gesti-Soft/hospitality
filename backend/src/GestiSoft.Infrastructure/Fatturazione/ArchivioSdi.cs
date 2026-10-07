using System.Text.Json;
using GestiSoft.Application.Fatturazione.Sdi;
using Microsoft.Extensions.Options;

namespace GestiSoft.Infrastructure.Fatturazione;

/// <summary>
/// Copia su disco delle consegne ricevute dallo SdI: la fattura, i metadati e un piccolo indice JSON,
/// in una cartella per identificativo. Il nome deriva dall'identificativo, così una consegna ritentata
/// riscrive gli stessi file invece di moltiplicarli. Scrittura su file temporaneo, flush su disco e
/// rinomina atomica: un file mezzo scritto è peggio di un file assente, perché sembra a posto.
/// </summary>
public class ArchivioSdi(IOptions<SdiOptions> options) : IArchivioSdi
{
    public async Task SalvaAsync(string identificativoSdi, bool prova, string nomeFile, byte[] file, string? nomeFileMetadati, byte[]? metadati, CancellationToken cancellationToken)
    {
        // Solo cifre nell'identificativo (lo schema lo vuole intero): nessun percorso costruito con testo esterno.
        var identificativo = new string(identificativoSdi.Where(char.IsAsciiDigit).ToArray());
        if (identificativo.Length == 0)
        {
            throw new IOException("Identificativo SdI non numerico: file non archiviato.");
        }

        var cartella = Path.Combine(options.Value.ArchivioPath, prova ? "collaudo" : "produzione", identificativo);
        Directory.CreateDirectory(cartella);

        await ScriviAtomicoAsync(Path.Combine(cartella, NomeSicuro(nomeFile, "fattura")), file, cancellationToken);
        if (metadati is not null)
        {
            await ScriviAtomicoAsync(Path.Combine(cartella, NomeSicuro(nomeFileMetadati, "metadati.xml")), metadati, cancellationToken);
        }

        var indice = JsonSerializer.SerializeToUtf8Bytes(new { identificativoSdi = identificativo, prova, nomeFile, nomeFileMetadati, ricevutoAtUtc = DateTime.UtcNow });
        await ScriviAtomicoAsync(Path.Combine(cartella, "indice.json"), indice, cancellationToken);
    }

    /// <summary>Il nome come lo manda lo SdI ([a-zA-Z0-9_.]): qualunque altro carattere si toglie.</summary>
    private static string NomeSicuro(string? nome, string ripiego)
    {
        var pulito = new string((nome ?? "").Where(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.').ToArray()).Trim('.');
        return pulito.Length == 0 ? ripiego : pulito;
    }

    private static async Task ScriviAtomicoAsync(string percorso, byte[] contenuto, CancellationToken cancellationToken)
    {
        var temporaneo = percorso + ".tmp";
        await using (var stream = new FileStream(temporaneo, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
        {
            await stream.WriteAsync(contenuto, cancellationToken);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporaneo, percorso, overwrite: true);
    }
}
