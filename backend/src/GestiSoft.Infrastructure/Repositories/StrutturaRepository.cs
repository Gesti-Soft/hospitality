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

    public async Task<IReadOnlyList<Struttura>> ListByClienteAsync(Guid? clienteId, CancellationToken cancellationToken)
    {
        // Le Strutture "eliminate" (soft-delete, Attivo=false) non compaiono mai nei selettori
        // operativi: restano nel database (nessun dato collegato va perso), consultabili solo
        // direttamente via GetByIdAsync/database se mai servisse riattivarle.
        var query = db.Strutture.AsNoTracking().Include(s => s.Cliente).Where(s => s.Attivo).AsQueryable();
        if (clienteId is { } id)
        {
            query = query.Where(s => s.ClienteId == id);
        }

        return await query.OrderBy(s => s.Nome).ToListAsync(cancellationToken);
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
