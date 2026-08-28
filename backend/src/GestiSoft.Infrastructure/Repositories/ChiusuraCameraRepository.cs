using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class ChiusuraCameraRepository(GestiSoftDbContext db) : IChiusuraCameraRepository
{
    public Task<ChiusuraCamera?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.ChiusureCamera.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ChiusuraCamera>> ListByCameraAsync(Guid cameraId, CancellationToken cancellationToken) =>
        await db.ChiusureCamera.AsNoTracking()
            .Where(c => c.CameraId == cameraId)
            .OrderBy(c => c.DataInizio)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ChiusuraCamera>> ListSovrapposteAsync(Guid cameraId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken) =>
        await db.ChiusureCamera.AsNoTracking()
            .Where(c => c.CameraId == cameraId && c.DataInizio <= dataFine && c.DataFine >= dataInizio)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ChiusuraCamera entity, CancellationToken cancellationToken)
    {
        db.ChiusureCamera.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ChiusuraCamera entity, CancellationToken cancellationToken)
    {
        db.ChiusureCamera.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
