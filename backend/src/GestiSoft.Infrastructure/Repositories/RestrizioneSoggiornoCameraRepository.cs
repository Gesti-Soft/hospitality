using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class RestrizioneSoggiornoCameraRepository(GestiSoftDbContext db) : IRestrizioneSoggiornoCameraRepository
{
    public Task<RestrizioneSoggiornoCamera?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.RestrizioniSoggiornoCamera.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RestrizioneSoggiornoCamera>> ListByCameraAsync(Guid cameraId, CancellationToken cancellationToken) =>
        await db.RestrizioniSoggiornoCamera.AsNoTracking()
            .Where(r => r.CameraId == cameraId)
            .OrderBy(r => r.DataInizio)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RestrizioneSoggiornoCamera>> ListSovrapposteAsync(Guid cameraId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken) =>
        await db.RestrizioniSoggiornoCamera.AsNoTracking()
            .Where(r => r.CameraId == cameraId && r.DataInizio <= dataFine && r.DataFine >= dataInizio)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(RestrizioneSoggiornoCamera entity, CancellationToken cancellationToken)
    {
        db.RestrizioniSoggiornoCamera.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(RestrizioneSoggiornoCamera entity, CancellationToken cancellationToken)
    {
        db.RestrizioniSoggiornoCamera.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
