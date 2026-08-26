using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Utenti;

public interface IUtenteStrutturaRepository
{
    Task<UtenteStruttura?> GetAsync(Guid utenteId, Guid strutturaId, CancellationToken cancellationToken);

    Task UpsertAsync(UtenteStruttura assegnazione, CancellationToken cancellationToken);
}
