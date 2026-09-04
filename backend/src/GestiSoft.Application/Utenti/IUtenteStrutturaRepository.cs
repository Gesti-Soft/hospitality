using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Utenti;

public interface IUtenteStrutturaRepository
{
    Task<UtenteStruttura?> GetAsync(Guid utenteId, Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Assegnazioni (con Utente incluso) per una Struttura — usata dalla schermata Utenti del frontend (Fase 9).</summary>
    Task<IReadOnlyList<UtenteStruttura>> ListByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Tutte le Strutture a cui un Utente ha accesso, su qualunque Cliente — usata al login per capire se TUTTE le sue Strutture hanno la licenza Wubook scaduta.</summary>
    Task<IReadOnlyList<UtenteStruttura>> ListByUtenteIdAsync(Guid utenteId, CancellationToken cancellationToken);

    /// <summary>True se l'utente ha il permesso SettingUser su almeno una Struttura di questo Cliente — usata per le azioni di gestione utenti non legate a una singola struttura (crea utente, modifica profilo, reset password).</summary>
    Task<bool> HaGestioneUtentiClienteAsync(Guid utenteId, Guid clienteId, CancellationToken cancellationToken);

    Task UpsertAsync(UtenteStruttura assegnazione, CancellationToken cancellationToken);

    /// <summary>Rimuove l'accesso di un Utente a una Struttura (l'Utente stesso resta, con le eventuali altre assegnazioni) — usata dalla schermata Utenti per "eliminare" un utente da questa Struttura.</summary>
    Task RemoveAsync(Guid utenteId, Guid strutturaId, CancellationToken cancellationToken);
}
