namespace GestiSoft.Application.PayTourist;

public record PayTouristEsitoOperazione(bool Ok, string? Errore);

public record PayTouristRiduzioneDto(int Id, string Nome, string? Descrizione, string? Percentuale);

public record PayTouristPortaleDto(int Id, string Nome);

public record PayTouristStrutturaRemotaDto(int Id, string Nome);

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
/// A differenza degli altri client, l'host non è un unico endpoint fisso: PayTourist assegna un
/// sottodominio per Comune (es. https://palermo.paytourist.com), quindi ogni chiamata riceve il
/// Comune Attività della Struttura chiamante e lo trasforma nel sottodominio giusto — non un
/// HttpClient.BaseAddress fissato una volta sola in DI come per Alloggiati Web/Osservatorio (dove
/// l'host è davvero lo stesso per tutte le Strutture).
/// </summary>
public interface IPayTouristClient
{
    /// <summary>Invia una singola prenotazione (un ospite/famiglia) — il legacy non ha mai raggruppato più prenotazioni in una chiamata, fedele qui.</summary>
    Task<PayTouristEsitoOperazione> InviaPrenotazioneAsync(string token, string? comuneAttivita, int idStruttura, int idSoftware, PayTouristReservationDto prenotazione, CancellationToken cancellationToken);

    Task<(bool Ok, IReadOnlyList<PayTouristRiduzioneDto> Riduzioni, string? Errore)> GetRiduzioniAsync(string token, string? comuneAttivita, int idStruttura, int idSoftware, CancellationToken cancellationToken);

    Task<(bool Ok, IReadOnlyList<PayTouristPortaleDto> Portali, string? Errore)> GetPortaliOnlineAsync(string token, string? comuneAttivita, int idStruttura, int idSoftware, CancellationToken cancellationToken);

    /// <summary>Elenco delle strutture abilitate su PayTourist per questo Token — usato per farle scegliere all'operatore invece di dover digitare a mano lo structure_id (vedi PayTouristConfigService.ListaStruttureDisponibiliAsync).</summary>
    Task<(bool Ok, IReadOnlyList<PayTouristStrutturaRemotaDto> Strutture, string? Errore)> GetStruttureAsync(string token, string? comuneAttivita, CancellationToken cancellationToken);

    /// <summary>Serializza una prenotazione nello stesso JSON che verrebbe inviato (campo "data" del multipart) — usato solo per l'export on-demand (mai inviato né persistito), garantisce che l'export mostri esattamente ciò che l'invio reale spedirebbe.</summary>
    string SerializzaPerExport(int idStruttura, int idSoftware, PayTouristReservationDto prenotazione);
}
