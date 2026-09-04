using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;

namespace GestiSoft.Infrastructure.Repositories;

public class RinnovoLicenzaRepository(GestiSoftDbContext db) : IRinnovoLicenzaRepository
{
    public async Task AddAsync(RinnovoLicenza rinnovo, CancellationToken cancellationToken)
    {
        db.RinnoviLicenza.Add(rinnovo);
        await db.SaveChangesAsync(cancellationToken);
    }
}
