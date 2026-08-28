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
    /// Ospiti con soggiorno non annullato e check-in nella finestra indicata (indipendentemente dal
    /// flag StatePolice) — per la schermata operativa Alloggiati Web (Impostazioni), che deve
    /// mostrare sia le schedine da inviare sia quelle già inviate, non solo quelle in coda.
    /// </summary>
    Task<IReadOnlyList<Ospite>> ListRecentiAlloggiatiWebAsync(Guid strutturaId, DateTime da, CancellationToken cancellationToken);

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

    /// <summary>
    /// Ospiti non annullati con check-in o check-out nella finestra indicata, sulle tipologie
    /// dell'appartamento — per la schermata operativa Osservatorio, che deve mostrare sia gli
    /// arrivi/partenze da inviare sia quelli già inviati (non solo il giorno corrente).
    /// </summary>
    Task<IReadOnlyList<Ospite>> ListRecentiOsservatorioAsync(Guid strutturaId, IReadOnlyCollection<Guid> tipologieIds, DateTime da, CancellationToken cancellationToken);

    /// <summary>
    /// Ospiti di prenotazioni già completate (check-out effettuato, <c>StatoPrenotazione.Completata</c>)
    /// non ancora inviate a PayTourist (<c>Prenotazione.PayTourist==false</c>), con check-out negli
    /// ultimi 7 giorni, sulle sole tipologie camera indicate (la "struttura PayTourist" a cui
    /// instradarli) — porta <c>OspitiLogic.GetPayTourist</c> del legacy 1:1, finestra mobile di 7
    /// giorni inclusa (nessun meccanismo di recupero oltre quella finestra, fedele al legacy — vedi
    /// PayTouristInvioService).
    /// </summary>
    Task<IReadOnlyList<Ospite>> ListDaInviarePayTouristAsync(Guid strutturaId, IReadOnlyCollection<Guid> tipologieIds, CancellationToken cancellationToken);

    /// <summary>
    /// Ospiti di prenotazioni completate con check-out nella finestra indicata (indipendentemente dal
    /// flag PayTourist), sulle tipologie della struttura PayTourist — per la schermata operativa
    /// PayTourist (Impostazioni), che deve mostrare sia le prenotazioni da inviare sia quelle già
    /// inviate, non solo quelle ancora in coda.
    /// </summary>
    Task<IReadOnlyList<Ospite>> ListRecentiPayTouristAsync(Guid strutturaId, IReadOnlyCollection<Guid> tipologieIds, DateTime da, CancellationToken cancellationToken);

    void Add(Ospite entity);

    /// <summary>
    /// Aggiunta esplicita di una riga Membro nuova a un Ospite già esistente e tracciato: la sola
    /// <c>ospite.Membri.Add(riga)</c> non basta in questo caso — con la chiave Guid già valorizzata
    /// lato client (vedi Entity.Id) e un genitore in stato Modified (non Added), EF Core la scopre
    /// via graph-fixup e la classifica come Modified invece che Added (la chiave non-default lo fa
    /// sembrare un record preesistente), generando un UPDATE su una riga che non esiste ancora e
    /// fallendo con DbUpdateConcurrencyException. Va marcata Added esplicitamente sul DbContext.
    /// </summary>
    void AddMembro(OspiteRiga riga);

    /// <summary>
    /// Rimozione esplicita di una riga Membro: OspiteId è nullable, quindi EF Core non la
    /// cancellerebbe automaticamente rimuovendola dalla collection Ospite.Membri (la orfanerebbe
    /// soltanto) — va marcata Removed esplicitamente sul DbContext.
    /// </summary>
    void RemoveMembro(OspiteRiga riga);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
