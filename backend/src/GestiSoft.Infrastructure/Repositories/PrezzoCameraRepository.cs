using GestiSoft.Application.Camere;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class PrezzoCameraRepository(GestiSoftDbContext db) : IPrezzoCameraRepository
{
    public Task<GestionePrezzo?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.PrezziCamera.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GestionePrezzo>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.PrezziCamera.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId)
            .OrderBy(p => p.DataInizio)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<GestionePrezzo>> ListSovrappostiAsync(
        Guid strutturaId,
        Guid? cameraId,
        Guid? tipologiaId,
        DateTime dataInizio,
        DateTime dataFine,
        CancellationToken cancellationToken)
    {
        dataInizio = DateTime.SpecifyKind(dataInizio, DateTimeKind.Utc);
        dataFine = DateTime.SpecifyKind(dataFine, DateTimeKind.Utc);

        var query = db.PrezziCamera
            .Where(p => p.StrutturaId == strutturaId && p.DataInizio != null && p.DataFine != null)
            .Where(p => p.DataInizio!.Value.Date <= dataFine && p.DataFine!.Value.Date >= dataInizio);

        query = cameraId is { } camera
            ? query.Where(p => p.CameraId == camera)
            : query.Where(p => p.CameraId == null && p.TipologiaId == tipologiaId);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GestionePrezzo>> ListPerCalendarioAsync(
        Guid strutturaId,
        Guid cameraId,
        Guid? tipologiaId,
        DateTime dataInizio,
        DateTime dataFine,
        CancellationToken cancellationToken)
    {
        dataInizio = DateTime.SpecifyKind(dataInizio, DateTimeKind.Utc);
        dataFine = DateTime.SpecifyKind(dataFine, DateTimeKind.Utc);

        return await db.PrezziCamera.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId && p.DataInizio != null && p.DataFine != null)
            .Where(p => p.DataInizio!.Value.Date <= dataFine && p.DataFine!.Value.Date >= dataInizio)
            .Where(p => p.CameraId == cameraId || (p.CameraId == null && p.TipologiaId == tipologiaId))
            .ToListAsync(cancellationToken);
    }

    public void Add(GestionePrezzo entity) => db.PrezziCamera.Add(entity);

    public void Remove(GestionePrezzo entity) => db.PrezziCamera.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
