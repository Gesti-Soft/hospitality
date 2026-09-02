using GestiSoft.Application.PayTourist;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class PayTouristStrutturaRepository(GestiSoftDbContext db) : IPayTouristStrutturaRepository
{
    public async Task<IReadOnlyList<PayTouristStruttura>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.PayTouristStrutture.AsNoTracking()
            .Include(p => p.Tipologie)
            .Where(p => p.StrutturaId == strutturaId)
            .ToListAsync(cancellationToken);

    public Task<PayTouristStruttura?> GetAsync(Guid strutturaId, Guid payTouristStrutturaId, CancellationToken cancellationToken) =>
        db.PayTouristStrutture
            .Include(p => p.Tipologie)
            .FirstOrDefaultAsync(p => p.StrutturaId == strutturaId && p.Id == payTouristStrutturaId, cancellationToken);

    public async Task AddAsync(PayTouristStruttura entity, CancellationToken cancellationToken)
    {
        db.PayTouristStrutture.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PayTouristStruttura entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.PayTouristStrutture.Update(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PayTouristStruttura entity, CancellationToken cancellationToken)
    {
        db.PayTouristStrutture.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public void RimuoviTipologia(PayTouristStrutturaTipologia riga) => db.PayTouristStruttureTipologie.Remove(riga);
}
