using GestiSoft.Application.Assistenza;
using Microsoft.Extensions.Logging;

namespace GestiSoft.Infrastructure.Assistenza;

/// <summary>
/// Foto dei ticket in una cartella del server (in produzione un volume Docker dedicato, vedi
/// docker-compose.yml). Una sottocartella per Struttura e un nome file generato qui: il nome scelto
/// da chi carica non tocca mai il disco.
/// </summary>
public class FileSystemAllegatiStorage(string cartellaRadice, ILogger<FileSystemAllegatiStorage> logger) : IAllegatiStorage
{
    private readonly string radice = Path.GetFullPath(cartellaRadice);

    public async Task<string> SalvaAsync(Guid strutturaId, byte[] contenuto, string estensione, CancellationToken cancellationToken)
    {
        var percorso = $"{strutturaId:N}/{Guid.NewGuid():N}{estensione}";
        var completo = PercorsoCompleto(percorso);

        Directory.CreateDirectory(Path.GetDirectoryName(completo)!);
        await File.WriteAllBytesAsync(completo, contenuto, cancellationToken);
        return percorso;
    }

    public Stream? Apri(string percorso)
    {
        var completo = PercorsoCompleto(percorso);
        return File.Exists(completo) ? File.OpenRead(completo) : null;
    }

    public bool Elimina(string percorso)
    {
        try
        {
            File.Delete(PercorsoCompleto(percorso));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Impossibile cancellare l'allegato {Percorso} dalla cartella degli allegati", percorso);
            return false;
        }
    }

    // Il percorso lo genera SalvaAsync, ma viene riletto dal database: se mai puntasse fuori dalla
    // cartella (riga alterata a mano) non si legge né si cancella nulla altrove sul disco.
    private string PercorsoCompleto(string percorso)
    {
        var completo = Path.GetFullPath(Path.Combine(radice, percorso));
        if (!completo.StartsWith(radice + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Percorso dell'allegato fuori dalla cartella degli allegati.");
        }

        return completo;
    }
}
