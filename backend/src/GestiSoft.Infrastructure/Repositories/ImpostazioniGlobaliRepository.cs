using GestiSoft.Application.SuperAdmin;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class ImpostazioniGlobaliRepository(GestiSoftDbContext db) : IImpostazioniGlobaliRepository
{
    public Task<ImpostazioniGlobali?> GetAsync(CancellationToken cancellationToken) =>
        db.ImpostazioniGlobali.FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertAsync(ImpostazioniGlobali entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.ImpostazioniGlobali.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
