using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Auth;

public interface IUtenteRepository
{
    Task<Utente?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<Utente?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Utente utente, CancellationToken cancellationToken);

    Task UpdateAsync(Utente utente, CancellationToken cancellationToken);
}
