using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class WubookIntegrazioneRepository(GestiSoftDbContext db) : IWubookIntegrazioneRepository
{
    public Task<WubookIntegrazione?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        db.WubookIntegrazioni.FirstOrDefaultAsync(w => w.StrutturaId == strutturaId, cancellationToken);

    // Come ImpostazioniStrutturaRepository: il toggle self-service (Attivo) da solo non basta, va
    // combinato con la concessione del Super Admin (Struttura.WubookAbilitato) altrimenti i job
    // schedulati continuano a tentare per una struttura revocata, bloccati solo a valle da
    // ConcessioneServiziGuard con relativo rumore nei log.
    public async Task<IReadOnlyList<WubookIntegrazione>> ListAttiveAsync(CancellationToken cancellationToken) =>
        await db.WubookIntegrazioni.AsNoTracking()
            .Where(w => w.Attivo)
            .Join(db.Strutture.AsNoTracking(), w => w.StrutturaId, s => s.Id, (w, s) => new { w, s.WubookAbilitato })
            .Where(x => x.WubookAbilitato)
            .Select(x => x.w)
            .ToListAsync(cancellationToken);

    public async Task UpsertAsync(WubookIntegrazione entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.WubookIntegrazioni.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
