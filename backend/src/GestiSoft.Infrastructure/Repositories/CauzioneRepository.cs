using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class CauzioneRepository(GestiSoftDbContext db) : ICauzioneRepository
{
    public async Task AddAsync(Cauzione entity, CancellationToken cancellationToken)
    {
        db.Cauzioni.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Cauzione>> ListByStrutturaAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        var query = db.Cauzioni.AsNoTracking().Where(c => c.StrutturaId == strutturaId);
        if (anno is { } a)
        {
            query = query.Where(c => c.DataInserimento != null && c.DataInserimento.Value.Year == a);
        }

        return await query.OrderByDescending(c => c.DataInserimento).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> ListaAnniConDatiAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.Cauzioni.AsNoTracking()
            .Where(c => c.StrutturaId == strutturaId && c.DataInserimento != null)
            .Select(c => c.DataInserimento!.Value.Year)
            .Distinct()
            .ToListAsync(cancellationToken);
}
