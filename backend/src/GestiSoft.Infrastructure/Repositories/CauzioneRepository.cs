using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;

namespace GestiSoft.Infrastructure.Repositories;

public class CauzioneRepository(GestiSoftDbContext db) : ICauzioneRepository
{
    public async Task AddAsync(Cauzione entity, CancellationToken cancellationToken)
    {
        db.Cauzioni.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
