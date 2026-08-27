using GestiSoft.Application.Impostazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class ImpostazioniStrutturaRepository(GestiSoftDbContext db) : IImpostazioniStrutturaRepository
{
    public Task<ImpostazioniStruttura?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        db.ImpostazioniStruttura.FirstOrDefaultAsync(i => i.StrutturaId == strutturaId, cancellationToken);

    public async Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerPoliziaAsync(CancellationToken cancellationToken) =>
        await db.ImpostazioniStruttura.AsNoTracking().Where(i => i.PoliziaStatoAttiva).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerOsservatorioAsync(CancellationToken cancellationToken) =>
        await db.ImpostazioniStruttura.AsNoTracking().Where(i => i.OsservatorioAttivo).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerPayTouristAsync(CancellationToken cancellationToken) =>
        await db.ImpostazioniStruttura.AsNoTracking().Where(i => i.PayTouristAttivo).ToListAsync(cancellationToken);

    public async Task UpsertAsync(ImpostazioniStruttura impostazioni, CancellationToken cancellationToken)
    {
        // Se l'entità è già tracciata (proviene da GetByStrutturaIdAsync sullo stesso DbContext
        // di richiesta, poi modificata dal service) EF Core rileva già le modifiche da solo:
        // serve solo aggiungerla esplicitamente quando è nuova (mai vista dal DbContext).
        if (db.Entry(impostazioni).State == EntityState.Detached)
        {
            db.ImpostazioniStruttura.Add(impostazioni);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
