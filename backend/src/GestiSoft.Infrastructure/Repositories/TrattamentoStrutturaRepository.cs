using GestiSoft.Application.Trattamenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class TrattamentoStrutturaRepository(GestiSoftDbContext db) : ITrattamentoStrutturaRepository
{
    public async Task<IReadOnlyList<TrattamentoStruttura>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.TrattamentiStruttura.AsNoTracking()
            .Where(t => t.StrutturaId == strutturaId)
            .OrderBy(t => t.Tipo)
            .ToListAsync(cancellationToken);

    public Task<TrattamentoStruttura?> GetAsync(Guid strutturaId, TipoTrattamento tipo, CancellationToken cancellationToken) =>
        db.TrattamentiStruttura.FirstOrDefaultAsync(t => t.StrutturaId == strutturaId && t.Tipo == tipo, cancellationToken);

    public async Task UpsertAsync(TrattamentoStruttura entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.TrattamentiStruttura.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
