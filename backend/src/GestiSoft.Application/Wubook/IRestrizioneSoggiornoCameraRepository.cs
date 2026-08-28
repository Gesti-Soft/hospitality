using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public interface IRestrizioneSoggiornoCameraRepository
{
    Task<RestrizioneSoggiornoCamera?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RestrizioneSoggiornoCamera>> ListByCameraAsync(Guid cameraId, CancellationToken cancellationToken);

    /// <summary>Regole di una camera che si sovrappongono al periodo indicato, per ricalcolare le restrizioni da inviare a Wubook.</summary>
    Task<IReadOnlyList<RestrizioneSoggiornoCamera>> ListSovrapposteAsync(Guid cameraId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken);

    Task AddAsync(RestrizioneSoggiornoCamera entity, CancellationToken cancellationToken);

    Task DeleteAsync(RestrizioneSoggiornoCamera entity, CancellationToken cancellationToken);
}
