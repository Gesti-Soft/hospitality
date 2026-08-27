using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Finanze;

public interface ISpesaRepository
{
    Task<Spesa?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Spesa>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken);

    Task AddAsync(Spesa entity, CancellationToken cancellationToken);

    Task UpdateAsync(Spesa entity, CancellationToken cancellationToken);

    Task DeleteAsync(Spesa entity, CancellationToken cancellationToken);
}
