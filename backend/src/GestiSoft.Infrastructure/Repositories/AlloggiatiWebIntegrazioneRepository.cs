using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class AlloggiatiWebIntegrazioneRepository(GestiSoftDbContext db) : IAlloggiatiWebIntegrazioneRepository
{
    public Task<AlloggiatiWebIntegrazione?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        db.AlloggiatiWebIntegrazioni.FirstOrDefaultAsync(a => a.StrutturaId == strutturaId, cancellationToken);

    public async Task UpsertAsync(AlloggiatiWebIntegrazione entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.AlloggiatiWebIntegrazioni.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
