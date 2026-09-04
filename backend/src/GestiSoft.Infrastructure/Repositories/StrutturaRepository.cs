using GestiSoft.Application.Auth;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class StrutturaRepository(GestiSoftDbContext db) : IStrutturaRepository
{
    public Task<Guid?> GetClienteIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        db.Strutture
            .Where(s => s.Id == strutturaId)
            .Select(s => (Guid?)s.ClienteId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Struttura?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Strutture.Include(s => s.Cliente).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Struttura>> ListByClienteAsync(Guid? clienteId, bool includiInattive, CancellationToken cancellationToken)
    {
        // Le Strutture "eliminate" (soft-delete, Attivo=false) non compaiono mai nei selettori
        // operativi di un Cliente: restano nel database (nessun dato collegato va perso),
        // consultabili solo direttamente via GetByIdAsync/database se mai servisse riattivarle. Il
        // Super Admin (unico chiamante con includiInattive=true) ha invece libero accesso anche a
        // quelle, per poterle riattivare/gestire dal pannello operativo.
        var query = db.Strutture.AsNoTracking().Include(s => s.Cliente).AsQueryable();
        if (!includiInattive)
        {
            query = query.Where(s => s.Attivo);
        }

        if (clienteId is { } id)
        {
            query = query.Where(s => s.ClienteId == id);
        }

        return await query.OrderBy(s => s.Nome).ToListAsync(cancellationToken);
    }

    /// <summary>Strutture attive a cui un utente normale (non titolare) ha un'assegnazione UtenteStruttura esplicita.</summary>
    public async Task<IReadOnlyList<Struttura>> ListAssegnateAsync(Guid utenteId, CancellationToken cancellationToken)
    {
        // Include non è applicabile dopo un Select che proietta un'altra entità (EF Core lo rifiuta a
        // runtime): si parte quindi dalla Struttura, filtrando su un sotto-query degli Id assegnati,
        // invece di partire da UtenteStruttura e proiettare la sua navigazione Struttura.
        var strutturaIdAssegnate = db.UtentiStrutture.Where(us => us.UtenteId == utenteId).Select(us => us.StrutturaId);

        return await db.Strutture
            .AsNoTracking()
            .Include(s => s.Cliente)
            .Where(s => s.Attivo && strutturaIdAssegnate.Contains(s.Id))
            .OrderBy(s => s.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Struttura struttura, CancellationToken cancellationToken)
    {
        db.Strutture.Add(struttura);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Struttura struttura, CancellationToken cancellationToken)
    {
        if (db.Entry(struttura).State == EntityState.Detached)
        {
            db.Strutture.Update(struttura);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
