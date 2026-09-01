using GestiSoft.Application.Impostazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class ImpostazioniStrutturaRepository(GestiSoftDbContext db) : IImpostazioniStrutturaRepository
{
    public Task<ImpostazioniStruttura?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        db.ImpostazioniStruttura.FirstOrDefaultAsync(i => i.StrutturaId == strutturaId, cancellationToken);

    // Il toggle self-service (PoliziaStatoAttiva/OsservatorioAttivo/PayTouristAttivo) da solo non
    // basta: se il Super Admin revoca il servizio dopo che il toggle era già stato attivato, questa
    // query deve escludere comunque la struttura, altrimenti il job tenta l'invio a ogni giro e
    // fallisce con un ForbiddenException da ConcessioneServiziGuard (bloccato correttamente a valle,
    // ma con rumore nei log a ogni esecuzione invece che una query pulita).
    public async Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerPoliziaAsync(CancellationToken cancellationToken) =>
        await db.ImpostazioniStruttura.AsNoTracking()
            .Where(i => i.PoliziaStatoAttiva)
            .Join(db.Strutture.AsNoTracking(), i => i.StrutturaId, s => s.Id, (i, s) => new { i, s.AlloggiatiWebAbilitato })
            .Where(x => x.AlloggiatiWebAbilitato)
            .Select(x => x.i)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerOsservatorioAsync(CancellationToken cancellationToken) =>
        await db.ImpostazioniStruttura.AsNoTracking()
            .Where(i => i.OsservatorioAttivo)
            .Join(db.Strutture.AsNoTracking(), i => i.StrutturaId, s => s.Id, (i, s) => new { i, s.OsservatorioAbilitato })
            .Where(x => x.OsservatorioAbilitato)
            .Select(x => x.i)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerPayTouristAsync(CancellationToken cancellationToken) =>
        await db.ImpostazioniStruttura.AsNoTracking()
            .Where(i => i.PayTouristAttivo)
            .Join(db.Strutture.AsNoTracking(), i => i.StrutturaId, s => s.Id, (i, s) => new { i, s.PayTouristAbilitato })
            .Where(x => x.PayTouristAbilitato)
            .Select(x => x.i)
            .ToListAsync(cancellationToken);

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
