using GestiSoft.Application.Osservatorio;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class OsservatorioInvioRepository(GestiSoftDbContext db) : IOsservatorioInvioRepository
{
    public async Task<IReadOnlyList<OsservatorioInvio>> ListByPrenotazioneAsync(Guid prenotazioneId, CancellationToken cancellationToken) =>
        await db.OsservatorioInvii.AsNoTracking()
            .Where(o => o.PrenotazioneId == prenotazioneId)
            .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(IReadOnlyList<OsservatorioInvio> righe, CancellationToken cancellationToken)
    {
        db.OsservatorioInvii.AddRange(righe);
        await db.SaveChangesAsync(cancellationToken);
    }
}
