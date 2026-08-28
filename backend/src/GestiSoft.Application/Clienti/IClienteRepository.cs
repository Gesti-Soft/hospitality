using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Clienti;

public interface IClienteRepository
{
    Task<Cliente?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Cliente>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(Cliente cliente, CancellationToken cancellationToken);

    Task UpdateAsync(Cliente cliente, CancellationToken cancellationToken);
}
