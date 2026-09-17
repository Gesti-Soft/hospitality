using GestiSoft.Application.PayTourist;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class PayTouristIntegrazioneRepository(GestiSoftDbContext db) : IPayTouristIntegrazioneRepository
{
    public Task<PayTouristIntegrazione?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        db.PayTouristIntegrazioni
            .Include(p => p.PortaliAttivi)
            .FirstOrDefaultAsync(p => p.StrutturaId == strutturaId, cancellationToken);

    public async Task UpsertAsync(PayTouristIntegrazione entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.PayTouristIntegrazioni.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
