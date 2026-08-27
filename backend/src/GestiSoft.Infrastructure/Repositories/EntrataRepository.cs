using GestiSoft.Application.Finanze;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class EntrataRepository(GestiSoftDbContext db) : IEntrataRepository
{
    public Task<Entrata?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Entrate.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Entrata>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        var query = db.Entrate.AsNoTracking().Where(e => e.StrutturaId == strutturaId);
        if (anno is { } a)
        {
            query = query.Where(e => e.Anno == a);
        }

        return await query.OrderByDescending(e => e.Data).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Entrata entity, CancellationToken cancellationToken)
    {
        db.Entrate.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Entrata entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.Entrate.Update(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Entrata entity, CancellationToken cancellationToken)
    {
        db.Entrate.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
