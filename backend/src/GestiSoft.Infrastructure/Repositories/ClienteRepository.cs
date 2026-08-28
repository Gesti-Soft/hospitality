using GestiSoft.Application.Clienti;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class ClienteRepository(GestiSoftDbContext db) : IClienteRepository
{
    public Task<Cliente?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Clienti.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Cliente>> ListAsync(CancellationToken cancellationToken) =>
        await db.Clienti.AsNoTracking().OrderBy(c => c.RagioneSociale).ToListAsync(cancellationToken);

    public async Task AddAsync(Cliente cliente, CancellationToken cancellationToken)
    {
        db.Clienti.Add(cliente);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Cliente cliente, CancellationToken cancellationToken)
    {
        if (db.Entry(cliente).State == EntityState.Detached)
        {
            db.Clienti.Update(cliente);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
