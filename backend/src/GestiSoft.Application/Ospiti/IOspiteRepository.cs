using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Ospiti;

public interface IOspiteRepository
{
    /// <summary>Testata Ospite con i Membri inclusi e tracciati, per una data Prenotazione.</summary>
    Task<Ospite?> GetByPrenotazioneAsync(Guid prenotazioneId, CancellationToken cancellationToken);

    /// <summary>
    /// Ospiti con soggiorno in corso, non ancora inviati ad Alloggiati Web, con check-in oggi o
    /// ieri — porta OspitiLogic.GetStatePolice del legacy (il giorno prima è incluso per coprire i
    /// check-in serali non ancora processati dal batch del giorno stesso). Membri e Prenotazione
    /// (per CheckIn) inclusi.
    /// </summary>
    Task<IReadOnlyList<Ospite>> ListDaInviareAlloggiatiWebAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>
    /// Ospiti con arrivo (check-in) in una data, soggiorno in corso, non ancora inviati
    /// all'Osservatorio Turistico (Prenotazione.PMS==false), sulle sole tipologie camera indicate
    /// (l'appartamento a cui instradarli) — porta OspitiLogic.GetPms del legacy, filtrato per
    /// appartamento invece che letto per intero.
    /// </summary>
    Task<IReadOnlyList<Ospite>> ListArriviOsservatorioAsync(Guid strutturaId, IReadOnlyCollection<Guid> tipologieIds, DateTime data, CancellationToken cancellationToken);

    /// <summary>
    /// Ospiti con partenza (check-out) in una data, già inviati come arrivo (Prenotazione.PMS==true),
    /// sulle sole tipologie camera indicate — porta OspitiLogic.GetStatePoliceOut del legacy (lì
    /// filtrato lato chiamante), nessun filtro sullo stato della prenotazione: la partenza va
    /// comunicata anche se l'operatore non ha ancora effettuato il check-out in reception.
    /// </summary>
    Task<IReadOnlyList<Ospite>> ListCheckoutOsservatorioAsync(Guid strutturaId, IReadOnlyCollection<Guid> tipologieIds, DateTime data, CancellationToken cancellationToken);

    void Add(Ospite entity);

    /// <summary>
    /// Rimozione esplicita di una riga Membro: OspiteId è nullable, quindi EF Core non la
    /// cancellerebbe automaticamente rimuovendola dalla collection Ospite.Membri (la orfanerebbe
    /// soltanto) — va marcata Removed esplicitamente sul DbContext.
    /// </summary>
    void RemoveMembro(OspiteRiga riga);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
