namespace GestiSoft.Application.PayTourist;

public record PayTouristEsitoOperazione(bool Ok, string? Errore);

public record PayTouristRiduzioneDto(int Id, string Nome);

public record PayTouristPortaleDto(int Id, string Nome);

public record PayTouristGuestDto(
    string PartnerId,
    string? Nome,
    string? Cognome,
    string? Email,
    DateTime CheckIn,
    DateTime CheckOut,
    int TypeId,
    int? DocumentType,
    string? DocumentNumber,
    int DocumentReleasedByCountry,
    int DocumentReleasedByCity,
    int Sex,
    int Nationality,
    int ResidenceCountry,
    int ResidenceCity,
    DateTime? DateOfBirth,
    int BirthCountry,
    int BirthCity,
    int? ReductionId);

public record PayTouristReservationDto(
    string PartnerId,
    DateTime CheckIn,
    DateTime CheckOut,
    int? OnlinePortalId,
    decimal? TotalFromOnlinePortal,
    string? OnlinePortalReservationId,
    IReadOnlyList<PayTouristGuestDto> Guests);

/// <summary>
/// Client REST/JSON verso PayTourist — porta la region "PayTourist" di StatePoliceApiRepository del
/// legacy (POST api/v1/reservations con corpo multipart/form-data — un campo "data" contenente il
/// JSON, non JSON diretto — GET api/v1/reductions, GET api/v1/online-portals-enabled). Autenticazione
/// con un bearer token statico per Struttura, nessun login/OAuth (a differenza di Osservatorio
/// Turistico). A differenza del legacy — che lasciava propagare un'eccezione da EnsureSuccessStatusCode
/// su reductions/online-portals — ogni fallimento di trasporto o risposta non 2xx viene qui
/// intercettato e tradotto in un esito "non ok", mai un'eccezione grezza: stesso principio già
/// applicato a tutti i client di questo progetto (Wubook/Alloggiati Web/Osservatorio).
/// </summary>
public interface IPayTouristClient
{
    /// <summary>Invia una singola prenotazione (un ospite/famiglia) — il legacy non ha mai raggruppato più prenotazioni in una chiamata, fedele qui.</summary>
    Task<PayTouristEsitoOperazione> InviaPrenotazioneAsync(string token, int idStruttura, int idSoftware, PayTouristReservationDto prenotazione, CancellationToken cancellationToken);

    Task<(bool Ok, IReadOnlyList<PayTouristRiduzioneDto> Riduzioni, string? Errore)> GetRiduzioniAsync(string token, int idStruttura, int idSoftware, CancellationToken cancellationToken);

    Task<(bool Ok, IReadOnlyList<PayTouristPortaleDto> Portali, string? Errore)> GetPortaliOnlineAsync(string token, int idStruttura, int idSoftware, CancellationToken cancellationToken);

    /// <summary>Serializza una prenotazione nello stesso JSON che verrebbe inviato (campo "data" del multipart) — usato solo per l'export on-demand (mai inviato né persistito), garantisce che l'export mostri esattamente ciò che l'invio reale spedirebbe.</summary>
    string SerializzaPerExport(int idStruttura, int idSoftware, PayTouristReservationDto prenotazione);
}
