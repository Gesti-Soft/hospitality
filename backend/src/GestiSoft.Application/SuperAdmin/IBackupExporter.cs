namespace GestiSoft.Application.SuperAdmin;

/// <summary>
/// Genera un dump completo del database al volo (via connessione di rete, non file già su
/// disco) per il download manuale dal pannello Super Admin — indipendente dal sistema di backup
/// automatico notturno (pgBackRest + pg_dump schedulato, vedi docs/backup-restore.md).
/// </summary>
public interface IBackupExporter
{
    Task<byte[]> EsportaAsync(CancellationToken cancellationToken);
}
