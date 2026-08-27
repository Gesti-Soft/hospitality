using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class DatiClienteRepository(GestiSoftDbContext db) : IDatiClienteRepository
{
    public Task<DatiCliente?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.DatiCliente.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<DatiCliente?> GetByCustomerKeyAsync(Guid strutturaId, string customerKey, CancellationToken cancellationToken) =>
        db.DatiCliente.FirstOrDefaultAsync(c => c.StrutturaId == strutturaId && c.CustomerKey == customerKey, cancellationToken);

    public async Task<IReadOnlyList<DatiCliente>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.DatiCliente.AsNoTracking()
            .Where(c => c.StrutturaId == strutturaId)
            .OrderBy(c => c.Denominazione).ThenBy(c => c.Cognome)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(DatiCliente entity, CancellationToken cancellationToken)
    {
        db.DatiCliente.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DatiCliente entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.DatiCliente.Update(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
