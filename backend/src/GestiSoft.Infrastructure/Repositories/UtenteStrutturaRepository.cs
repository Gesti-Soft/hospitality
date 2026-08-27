using GestiSoft.Application.Utenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class UtenteStrutturaRepository(GestiSoftDbContext db) : IUtenteStrutturaRepository
{
    public Task<UtenteStruttura?> GetAsync(Guid utenteId, Guid strutturaId, CancellationToken cancellationToken) =>
        db.UtentiStrutture.FirstOrDefaultAsync(us => us.UtenteId == utenteId && us.StrutturaId == strutturaId, cancellationToken);

    public async Task<IReadOnlyList<UtenteStruttura>> ListByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.UtentiStrutture.AsNoTracking().Include(us => us.Utente)
            .Where(us => us.StrutturaId == strutturaId)
            .ToListAsync(cancellationToken);

    public async Task UpsertAsync(UtenteStruttura assegnazione, CancellationToken cancellationToken)
    {
        // Come in ImpostazioniStrutturaRepository: un'entità già tracciata (arrivata da GetAsync
        // sullo stesso DbContext di richiesta) viene aggiornata automaticamente da EF Core;
        // serve solo Add esplicito quando è nuova (mai vista dal DbContext, quindi Detached —
        // il solo Id valorizzato di default NON basta a distinguerla, EF traccia lo stato reale).
        if (db.Entry(assegnazione).State == EntityState.Detached)
        {
            db.UtentiStrutture.Add(assegnazione);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
