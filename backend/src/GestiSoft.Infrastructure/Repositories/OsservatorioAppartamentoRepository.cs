using GestiSoft.Application.Osservatorio;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class OsservatorioAppartamentoRepository(GestiSoftDbContext db) : IOsservatorioAppartamentoRepository
{
    public async Task<IReadOnlyList<OsservatorioAppartamento>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.OsservatorioAppartamenti.AsNoTracking()
            .Include(a => a.Tipologie)
            .Where(a => a.StrutturaId == strutturaId)
            .ToListAsync(cancellationToken);

    public Task<OsservatorioAppartamento?> GetAsync(Guid strutturaId, Guid appartamentoId, CancellationToken cancellationToken) =>
        db.OsservatorioAppartamenti
            .Include(a => a.Tipologie)
            .FirstOrDefaultAsync(a => a.StrutturaId == strutturaId && a.Id == appartamentoId, cancellationToken);

    public async Task AddAsync(OsservatorioAppartamento entity, CancellationToken cancellationToken)
    {
        db.OsservatorioAppartamenti.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OsservatorioAppartamento entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.OsservatorioAppartamenti.Update(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public void RimuoviTipologia(OsservatorioAppartamentoTipologia riga) => db.OsservatorioAppartamentiTipologie.Remove(riga);

    public void AggiungiTipologia(OsservatorioAppartamentoTipologia riga) => db.OsservatorioAppartamentiTipologie.Add(riga);
}
