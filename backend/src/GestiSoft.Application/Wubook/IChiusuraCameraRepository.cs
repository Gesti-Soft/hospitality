using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public interface IChiusuraCameraRepository
{
    Task<ChiusuraCamera?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChiusuraCamera>> ListByCameraAsync(Guid cameraId, CancellationToken cancellationToken);

    /// <summary>Chiusure di una camera che si sovrappongono al periodo indicato, per ricalcolare la disponibilità da inviare a Wubook.</summary>
    Task<IReadOnlyList<ChiusuraCamera>> ListSovrapposteAsync(Guid cameraId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken);

    Task AddAsync(ChiusuraCamera entity, CancellationToken cancellationToken);

    Task DeleteAsync(ChiusuraCamera entity, CancellationToken cancellationToken);
}
