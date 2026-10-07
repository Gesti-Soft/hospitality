using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione.Sdi;

/// <summary>
/// Quello che il canale SdI non è riuscito a sistemare da solo, per il Super Admin: fatture ricevute
/// senza destinatario, ricevute che non si agganciano a nessuna fattura, consegne del collaudo.
/// Gli elenchi non si portano dietro i byte dei file: si scaricano uno per uno.
/// </summary>
public interface ICanaleSdiAdminRepository
{
    /// <summary>File ricevuti mai attribuiti a una struttura (senza destinatario, più destinatari, illeggibili), collaudo escluso.</summary>
    Task<IReadOnlyList<FileRicevutoSdi>> FileInQuarantenaAsync(CancellationToken cancellationToken);

    /// <summary>Gli ultimi file arrivati dallo SdI di collaudo.</summary>
    Task<IReadOnlyList<FileRicevutoSdi>> FileDiCollaudoAsync(int massimo, CancellationToken cancellationToken);

    /// <summary>Le ultime ricevute non agganciate a nessuna fattura spedita.</summary>
    Task<IReadOnlyList<RicevutaSdi>> RicevuteNonAbbinateAsync(int massimo, CancellationToken cancellationToken);

    /// <summary>Tracciato e coi byte: per scaricarlo o attribuirlo.</summary>
    Task<FileRicevutoSdi?> FileRicevutoAsync(Guid id, CancellationToken cancellationToken);

    Task<RicevutaSdi?> RicevutaAsync(Guid id, CancellationToken cancellationToken);
}
