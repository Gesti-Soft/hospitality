using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Auth;

/// <summary>Accesso a Struttura: sia le consultazioni minime per l'autorizzazione multi-tenant, sia la gestione CRUD.</summary>
public interface IStrutturaRepository
{
    Task<Guid?> GetClienteIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<Struttura?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Struttura>> ListByClienteAsync(Guid? clienteId, CancellationToken cancellationToken);

    Task AddAsync(Struttura struttura, CancellationToken cancellationToken);

    Task UpdateAsync(Struttura struttura, CancellationToken cancellationToken);
}
