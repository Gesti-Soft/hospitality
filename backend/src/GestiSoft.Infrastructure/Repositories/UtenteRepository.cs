using GestiSoft.Application.Auth;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class UtenteRepository(GestiSoftDbContext db) : IUtenteRepository
{
    public Task<Utente?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Utenti.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<Utente?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Utenti.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task AddAsync(Utente utente, CancellationToken cancellationToken)
    {
        db.Utenti.Add(utente);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Utente utente, CancellationToken cancellationToken)
    {
        if (db.Entry(utente).State == EntityState.Detached)
        {
            db.Utenti.Update(utente);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
