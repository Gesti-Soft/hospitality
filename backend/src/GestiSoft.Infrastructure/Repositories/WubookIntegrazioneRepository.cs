using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class WubookIntegrazioneRepository(GestiSoftDbContext db) : IWubookIntegrazioneRepository
{
    public Task<WubookIntegrazione?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        db.WubookIntegrazioni.FirstOrDefaultAsync(w => w.StrutturaId == strutturaId, cancellationToken);

    public async Task<IReadOnlyList<WubookIntegrazione>> ListAttiveAsync(CancellationToken cancellationToken) =>
        await db.WubookIntegrazioni.AsNoTracking().Where(w => w.Attivo).ToListAsync(cancellationToken);

    public async Task UpsertAsync(WubookIntegrazione entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.WubookIntegrazioni.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
