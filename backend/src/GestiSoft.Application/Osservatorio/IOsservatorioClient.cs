namespace GestiSoft.Application.Osservatorio;

public record OsservatorioLoginRisultato(bool Ok, string? Token, string? Errore);

public record OsservatorioEsitoOperazione(bool Ok, string? Errore);

public record OsservatorioRoomDto(string RoomId, DateTime StartDate, DateTime EndDate);

public record OsservatorioGuestDto(
    string GuestId,
    int Age,
    string NationalityCode,
    string BirthPlaceCode,
    string ResidencePlaceCode,
    int Type,
    int Gender,
    string? EMail,
    DateTime ArrivalDate,
    DateTime DepartureDate,
    bool Checkout,
    IReadOnlyList<OsservatorioRoomDto> Rooms);

public record OsservatorioStayDto(string StayId, IReadOnlyList<OsservatorioGuestDto> Guests);

/// <summary>
/// Client REST/XML verso l'Osservatorio Turistico — porta le region "Osservatorio Turistico" di
/// StatePoliceApiRepository del legacy (login/logout via header, enddayfrompms e stay/add-update
/// via XML scritto a mano con System.Xml.Linq, stesso approccio delle altre integrazioni di questo
/// progetto). A differenza del legacy, <see cref="SendArrivalsAsync"/>/<see cref="SendCheckoutsAsync"/>
/// accettano più Stay in una sola chiamata (il contratto XML del legacy — <c>RootObjectDTO.Stay</c>
/// — è già una lista, ma il legacy inviava sempre una chiamata per ospite): qui una sola chiamata
/// per appartamento per giorno raggruppa tutte le schede in arrivo/partenza di quel giorno,
/// riducendo drasticamente le andate e ritorno HTTP quando arrivano/partono più ospiti insieme
/// (miglioramento esplicitamente autorizzato dall'utente per questa fase, non verificato contro un
/// endpoint reale). Ogni fallimento di trasporto viene intercettato e tradotto in un risultato "non
/// ok" — mai un'eccezione che risale a un 500 grezzo, stesso principio delle altre integrazioni.
/// </summary>
public interface IOsservatorioClient
{
    Task<OsservatorioLoginRisultato> LoginAsync(string entityCode, string password, CancellationToken cancellationToken);

    Task LogoutAsync(string token, CancellationToken cancellationToken);

    Task<OsservatorioEsitoOperazione> EndDayAsync(string token, string hotelCode, DateTime data, CancellationToken cancellationToken);

    Task<OsservatorioEsitoOperazione> SendArrivalsAsync(string token, string hotelCode, IReadOnlyList<OsservatorioStayDto> stays, CancellationToken cancellationToken);

    Task<OsservatorioEsitoOperazione> SendCheckoutsAsync(string token, string hotelCode, IReadOnlyList<OsservatorioStayDto> stays, CancellationToken cancellationToken);

    /// <summary>
    /// GET entity/GetCurrentStatusDate/{hotelCode} — il PROSSIMO giorno da chiudere secondo il
    /// server Osservatorio per questa entità (data locale italiana, senza componente ora), non
    /// l'ultimo già chiuso. Autorevole, mai "oggi" secondo il nostro orologio: il server può essere
    /// "fermo" a un giorno diverso per ogni struttura (es. da un'installazione precedente mai
    /// proseguita) ed enddayfrompms rifiuta con "Invalid Date" se non si riparte esattamente da lì
    /// (verificato dal vivo: un +1 su questo valore veniva anch'esso rifiutato). Null se la chiamata
    /// fallisce o la risposta non è una data valida.
    /// </summary>
    Task<DateTime?> GetCurrentStatusDateAsync(string token, string hotelCode, CancellationToken cancellationToken);
}
