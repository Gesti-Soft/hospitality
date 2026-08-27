using GestiSoft.Application.Finanze;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class SpesaRepository(GestiSoftDbContext db) : ISpesaRepository
{
    public Task<Spesa?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Spese.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Spesa>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        var query = db.Spese.AsNoTracking().Where(s => s.StrutturaId == strutturaId);
        if (anno is { } a)
        {
            query = query.Where(s => s.Anno == a);
        }

        return await query.OrderByDescending(s => s.DataSpesa).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Spesa entity, CancellationToken cancellationToken)
    {
        db.Spese.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Spesa entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.Spese.Update(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Spesa entity, CancellationToken cancellationToken)
    {
        db.Spese.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
