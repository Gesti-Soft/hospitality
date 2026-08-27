using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione;

public interface IDatiClienteRepository
{
    Task<DatiCliente?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<DatiCliente?> GetByCustomerKeyAsync(Guid strutturaId, string customerKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<DatiCliente>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task AddAsync(DatiCliente entity, CancellationToken cancellationToken);

    Task UpdateAsync(DatiCliente entity, CancellationToken cancellationToken);
}
