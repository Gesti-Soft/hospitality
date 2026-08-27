using GestiSoft.Application.Camere;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class CameraRepository(GestiSoftDbContext db) : ICameraRepository
{
    public Task<SettingRoom?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Camere.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<SettingRoom?> GetByIdWubookAsync(Guid strutturaId, int idCameraWubook, CancellationToken cancellationToken) =>
        db.Camere.FirstOrDefaultAsync(r => r.StrutturaId == strutturaId && r.IdCameraWubook == idCameraWubook, cancellationToken);

    public async Task<IReadOnlyList<SettingRoom>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.Camere.AsNoTracking()
            .Include(r => r.Tipologia)
            .Where(r => r.StrutturaId == strutturaId)
            .OrderBy(r => r.Nome)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByNomeAsync(Guid strutturaId, string nome, Guid? escludiId, CancellationToken cancellationToken) =>
        db.Camere.AnyAsync(
            r => r.StrutturaId == strutturaId && r.Nome == nome && r.Id != (escludiId ?? Guid.Empty),
            cancellationToken);

    public async Task AddAsync(SettingRoom entity, CancellationToken cancellationToken)
    {
        db.Camere.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SettingRoom entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.Camere.Update(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(SettingRoom entity, CancellationToken cancellationToken)
    {
        db.Camere.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
