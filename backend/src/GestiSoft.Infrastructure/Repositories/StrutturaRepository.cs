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
        db.Strutture.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Struttura>> ListByClienteAsync(Guid? clienteId, CancellationToken cancellationToken)
    {
        var query = db.Strutture.AsNoTracking().AsQueryable();
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
}
