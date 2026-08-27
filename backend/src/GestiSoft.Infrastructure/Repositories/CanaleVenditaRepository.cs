using GestiSoft.Application.Camere;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class CanaleVenditaRepository(GestiSoftDbContext db) : ICanaleVenditaRepository
{
    public Task<SettingAgenzia?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.CanaliVendita.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SettingAgenzia>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.CanaliVendita.AsNoTracking()
            .Where(a => a.StrutturaId == strutturaId)
            .OrderBy(a => a.Descrizione)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByDescrizioneAsync(Guid strutturaId, string descrizione, Guid? escludiId, CancellationToken cancellationToken) =>
        db.CanaliVendita.AnyAsync(
            a => a.StrutturaId == strutturaId && a.Descrizione == descrizione && a.Id != (escludiId ?? Guid.Empty),
            cancellationToken);

    public async Task AddAsync(SettingAgenzia entity, CancellationToken cancellationToken)
    {
        db.CanaliVendita.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SettingAgenzia entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.CanaliVendita.Update(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(SettingAgenzia entity, CancellationToken cancellationToken)
    {
        db.CanaliVendita.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
