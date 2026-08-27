using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class DatiAziendaliRepository(GestiSoftDbContext db) : IDatiAziendaliRepository
{
    public Task<DatiAziendali?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        db.DatiAziendali.FirstOrDefaultAsync(d => d.StrutturaId == strutturaId, cancellationToken);

    public async Task UpsertAsync(DatiAziendali entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.DatiAziendali.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
