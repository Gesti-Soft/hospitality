using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Auth;

/// <summary>Accesso a Struttura: sia le consultazioni minime per l'autorizzazione multi-tenant, sia la gestione CRUD.</summary>
public interface IStrutturaRepository
{
    Task<Guid?> GetClienteIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<Struttura?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Strutture di un Cliente (o di tutti, se null) per i selettori operativi. <paramref name="includiInattive"/>
    /// va passato true solo per il Super Admin, che deve poter vedere/entrare anche in una Struttura
    /// disattivata (soft-delete) — per chiunque altro le Strutture disattivate restano sempre escluse.
    /// </summary>
    Task<IReadOnlyList<Struttura>> ListByClienteAsync(Guid? clienteId, bool includiInattive, CancellationToken cancellationToken);

    /// <summary>Strutture attive a cui un utente normale (non titolare) ha un'assegnazione UtenteStruttura esplicita.</summary>
    Task<IReadOnlyList<Struttura>> ListAssegnateAsync(Guid utenteId, CancellationToken cancellationToken);

    Task AddAsync(Struttura struttura, CancellationToken cancellationToken);

    Task UpdateAsync(Struttura struttura, CancellationToken cancellationToken);
}
