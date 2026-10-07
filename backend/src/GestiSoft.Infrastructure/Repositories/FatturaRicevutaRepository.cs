using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class FatturaRicevutaRepository(GestiSoftDbContext db) : IFatturaRicevutaRepository
{
    public async Task<IReadOnlyList<FatturaRicevuta>> ListAsync(IReadOnlyCollection<Guid> strutturaIds, int? anno, CancellationToken cancellationToken)
    {
        var query = db.FattureRicevute.AsNoTracking().Where(f => strutturaIds.Contains(f.StrutturaId));
        if (anno is { } a)
        {
            query = query.Where(f => f.Data >= new DateOnly(a, 1, 1) && f.Data <= new DateOnly(a, 12, 31));
        }

        return await query.OrderByDescending(f => f.Data).ThenByDescending(f => f.RicevutaAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> ListaAnniAsync(IReadOnlyCollection<Guid> strutturaIds, CancellationToken cancellationToken) =>
        await db.FattureRicevute.AsNoTracking()
            .Where(f => strutturaIds.Contains(f.StrutturaId))
            .Select(f => f.Data.Year)
            .Distinct()
            .OrderByDescending(a => a)
            .ToListAsync(cancellationToken);

    public Task<FatturaRicevuta?> GetAsync(IReadOnlyCollection<Guid> strutturaIds, Guid id, CancellationToken cancellationToken) =>
        db.FattureRicevute.FirstOrDefaultAsync(f => f.Id == id && strutturaIds.Contains(f.StrutturaId), cancellationToken);

    public async Task<(string NomeFile, byte[] File)?> GetFileAsync(Guid fileRicevutoSdiId, CancellationToken cancellationToken)
    {
        var file = await db.FileRicevutiSdi.AsNoTracking()
            .Where(f => f.Id == fileRicevutoSdiId)
            .Select(f => new { f.NomeFile, f.File })
            .FirstOrDefaultAsync(cancellationToken);
        return file is null ? null : (file.NomeFile, file.File);
    }

    public Task SalvaAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
