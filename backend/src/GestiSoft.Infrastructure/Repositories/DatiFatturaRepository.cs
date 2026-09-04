using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestiSoft.Infrastructure.Repositories;

public class DatiFatturaRepository(GestiSoftDbContext db) : IDatiFatturaRepository
{
    public Task<DatiFattura?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.DatiFattura.Include(f => f.Cliente).FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<DatiFattura?> GetByPrenotazioneIdAsync(Guid prenotazioneId, CancellationToken cancellationToken) =>
        db.DatiFattura.AsNoTracking().Include(f => f.Cliente)
            .Where(f => f.PrenotazioneId == prenotazioneId)
            .OrderByDescending(f => f.DataDocumento)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DatiFattura>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        var query = db.DatiFattura.AsNoTracking().Include(f => f.Cliente).Where(f => f.StrutturaId == strutturaId);
        if (anno is { } a)
        {
            query = query.Where(f => f.Anno == a);
        }

        return await query.OrderByDescending(f => f.Progressivo).ToListAsync(cancellationToken);
    }

    public async Task<int> GetMaxProgressivoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        var max = await db.DatiFattura
            .Where(f => f.StrutturaId == strutturaId && f.Anno == anno)
            .Select(f => (int?)f.Progressivo)
            .MaxAsync(cancellationToken);

        return max ?? 0;
    }

    public async Task<bool> TryAddAsync(DatiFattura entity, CancellationToken cancellationToken)
    {
        db.DatiFattura.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    public async Task UpdateAsync(DatiFattura entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.DatiFattura.Update(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
