using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Ospiti;

public interface IOspiteRepository
{
    /// <summary>Testata Ospite con i Membri inclusi e tracciati, per una data Prenotazione.</summary>
    Task<Ospite?> GetByPrenotazioneAsync(Guid prenotazioneId, CancellationToken cancellationToken);

    void Add(Ospite entity);

    /// <summary>
    /// Rimozione esplicita di una riga Membro: OspiteId è nullable, quindi EF Core non la
    /// cancellerebbe automaticamente rimuovendola dalla collection Ospite.Membri (la orfanerebbe
    /// soltanto) — va marcata Removed esplicitamente sul DbContext.
    /// </summary>
    void RemoveMembro(OspiteRiga riga);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
