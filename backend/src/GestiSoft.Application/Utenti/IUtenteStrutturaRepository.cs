using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Utenti;

public interface IUtenteStrutturaRepository
{
    Task<UtenteStruttura?> GetAsync(Guid utenteId, Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Assegnazioni (con Utente incluso) per una Struttura — usata dalla schermata Utenti del frontend (Fase 9).</summary>
    Task<IReadOnlyList<UtenteStruttura>> ListByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task UpsertAsync(UtenteStruttura assegnazione, CancellationToken cancellationToken);
}
