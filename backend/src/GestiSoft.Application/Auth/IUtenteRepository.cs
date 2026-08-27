using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Auth;

public interface IUtenteRepository
{
    Task<Utente?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<Utente?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Utenti di un Cliente — usata dalla schermata Utenti del frontend (Fase 9) per popolare l'elenco e il selettore "assegna a struttura".</summary>
    Task<IReadOnlyList<Utente>> ListByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken);

    Task AddAsync(Utente utente, CancellationToken cancellationToken);

    Task UpdateAsync(Utente utente, CancellationToken cancellationToken);
}
