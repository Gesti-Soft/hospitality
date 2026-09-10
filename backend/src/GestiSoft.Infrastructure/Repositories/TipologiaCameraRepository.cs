using GestiSoft.Application.Camere;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class TipologiaCameraRepository(GestiSoftDbContext db) : ITipologiaCameraRepository
{
    public Task<SettingTipologia?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.TipologieCamera.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<SettingTipologia?> GetByIdWubookAsync(Guid strutturaId, int idCameraWubook, CancellationToken cancellationToken) =>
        db.TipologieCamera.FirstOrDefaultAsync(t => t.StrutturaId == strutturaId && t.IdCameraWubook == idCameraWubook, cancellationToken);

    public async Task<IReadOnlyList<SettingTipologia>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.TipologieCamera.AsNoTracking()
            .Where(t => t.StrutturaId == strutturaId)
            .OrderBy(t => t.TipologiaCamera)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByNomeAsync(Guid strutturaId, string tipologiaCamera, Guid? escludiId, CancellationToken cancellationToken) =>
        db.TipologieCamera.AnyAsync(
            t => t.StrutturaId == strutturaId && t.TipologiaCamera == tipologiaCamera && t.Id != (escludiId ?? Guid.Empty),
            cancellationToken);

    public async Task AddAsync(SettingTipologia entity, CancellationToken cancellationToken)
    {
        db.TipologieCamera.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SettingTipologia entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.TipologieCamera.Update(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(SettingTipologia entity, CancellationToken cancellationToken)
    {
        db.TipologieCamera.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
