using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestiSoft.Infrastructure.Repositories;

public class DatiFatturaRepository(GestiSoftDbContext db) : IDatiFatturaRepository
{
    public Task<DatiFattura?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.DatiFattura.Include(f => f.Cliente).Include(f => f.Righe.OrderBy(r => r.Numero)).Include(f => f.Prenotazioni)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<DatiFattura?> GetByPrenotazioneIdAsync(Guid prenotazioneId, CancellationToken cancellationToken) =>
        db.DatiFattura.AsNoTracking().Include(f => f.Cliente).Include(f => f.Righe.OrderBy(r => r.Numero)).Include(f => f.Prenotazioni)
            .AsSplitQuery()
            .Where(f => f.PrenotazioneId == prenotazioneId || f.Prenotazioni.Any(p => p.PrenotazioneId == prenotazioneId))
            .OrderByDescending(f => f.DataDocumento)
            .ThenByDescending(f => f.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DatiFattura>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        var query = db.DatiFattura.AsNoTracking().Include(f => f.Cliente).Include(f => f.Righe.OrderBy(r => r.Numero)).Include(f => f.Prenotazioni)
            .AsSplitQuery()
            .Where(f => f.StrutturaId == strutturaId);
        if (anno is { } a)
        {
            query = query.Where(f => f.Anno == a);
        }

        return await query.OrderByDescending(f => f.Progressivo).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> ListaAnniConDatiAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.DatiFattura.AsNoTracking()
            .Where(f => f.StrutturaId == strutturaId)
            .Select(f => f.Anno)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<int> GetMaxProgressivoAsync(Guid strutturaId, int anno, TipoEmissioneDocumento tipoEmissione, CancellationToken cancellationToken)
    {
        var max = await db.DatiFattura
            .Where(f => f.StrutturaId == strutturaId && f.Anno == anno && f.TipoEmissione == tipoEmissione)
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

    public async Task SostituisciRigheAsync(DatiFattura entity, List<RigaFattura> righe, CancellationToken cancellationToken)
    {
        var esistenti = await db.RigheFattura.Where(r => r.DatiFatturaId == entity.Id).ToListAsync(cancellationToken);
        db.RigheFattura.RemoveRange(esistenti);
        foreach (var riga in righe)
        {
            riga.DatiFatturaId = entity.Id;
        }

        db.RigheFattura.AddRange(righe);
        entity.Righe = righe;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RigaFatturata>> ListRigheFatturateAsync(Guid strutturaId, IReadOnlyCollection<Guid> prenotazioneIds, Guid? escludiFatturaId, CancellationToken cancellationToken) =>
        await (from riga in db.RigheFattura.AsNoTracking()
               join fattura in db.DatiFattura.AsNoTracking() on riga.DatiFatturaId equals fattura.Id
               where riga.StrutturaId == strutturaId
                     && riga.PrenotazioneId != null
                     && prenotazioneIds.Contains(riga.PrenotazioneId.Value)
                     && riga.Tipo != TipoRigaFattura.Altro
                     && fattura.Id != (escludiFatturaId ?? Guid.Empty)
               select new RigaFatturata(riga.PrenotazioneId!.Value, riga.Tipo, riga.PrenotazioneServizioId, fattura.TipoEmissione, fattura.NumeroDocumento, fattura.Anno))
            .ToListAsync(cancellationToken);
}
