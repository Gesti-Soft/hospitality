using GestiSoft.Application.Auth;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class DueFattoriRepository(GestiSoftDbContext db) : IDueFattoriRepository
{
    public async Task<IReadOnlyList<CodiceRecuperoUtente>> ListCodiciNonUsatiAsync(Guid utenteId, CancellationToken cancellationToken) =>
        await db.CodiciRecuperoUtente
            .Where(c => c.UtenteId == utenteId && c.UsatoAtUtc == null)
            .ToListAsync(cancellationToken);

    public async Task SostituisciCodiciAsync(Guid utenteId, IReadOnlyList<CodiceRecuperoUtente> nuovi, CancellationToken cancellationToken)
    {
        await db.CodiciRecuperoUtente.Where(c => c.UtenteId == utenteId).ExecuteDeleteAsync(cancellationToken);
        db.CodiciRecuperoUtente.AddRange(nuovi);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SegnaCodiceUsatoAsync(CodiceRecuperoUtente codice, CancellationToken cancellationToken)
    {
        codice.UsatoAtUtc = DateTime.UtcNow;
        if (db.Entry(codice).State == EntityState.Detached)
        {
            db.CodiciRecuperoUtente.Update(codice);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<DispositivoFidato?> GetDispositivoAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.DispositiviFidati.AsNoTracking().FirstOrDefaultAsync(d => d.TokenHash == tokenHash, cancellationToken);

    public async Task AddDispositivoAsync(DispositivoFidato dispositivo, CancellationToken cancellationToken)
    {
        db.DispositiviFidati.Add(dispositivo);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RimuoviDispositiviAsync(Guid utenteId, CancellationToken cancellationToken) =>
        await db.DispositiviFidati.Where(d => d.UtenteId == utenteId).ExecuteDeleteAsync(cancellationToken);

    public async Task RimuoviDispositiviScadutiAsync(CancellationToken cancellationToken) =>
        await db.DispositiviFidati.Where(d => d.ScadeAtUtc < DateTime.UtcNow).ExecuteDeleteAsync(cancellationToken);
}
