using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class WubookEventoRicevutoRepository(GestiSoftDbContext db) : IWubookEventoRicevutoRepository
{
    public Task<WubookEventoRicevuto?> GetByRcodeAsync(Guid strutturaId, int rcode, CancellationToken cancellationToken) =>
        db.WubookEventiRicevuti.FirstOrDefaultAsync(e => e.StrutturaId == strutturaId && e.Rcode == rcode, cancellationToken);

    public async Task<IReadOnlyList<WubookEventoRicevuto>> ListByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.WubookEventiRicevuti.AsNoTracking()
            .Where(e => e.StrutturaId == strutturaId)
            .OrderByDescending(e => e.UpdatedAtUtc ?? e.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

    public async Task UpsertAsync(WubookEventoRicevuto evento, CancellationToken cancellationToken)
    {
        if (db.Entry(evento).State == EntityState.Detached)
        {
            db.WubookEventiRicevuti.Add(evento);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
