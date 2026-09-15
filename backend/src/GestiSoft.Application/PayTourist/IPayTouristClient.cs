namespace GestiSoft.Application.PayTourist;

public record PayTouristEsitoOperazione(bool Ok, string? Errore);

public record PayTouristRiduzioneDto(int Id, string Nome, string? Descrizione, string? Percentuale);

public record PayTouristPortaleDto(int Id, string Nome);

public record PayTouristStrutturaRemotaDto(int Id, string Nome);

/// <summary>Un ospite come lo restituisce il portale: nome e cognome arrivano in un campo unico, senza distinguerli.</summary>
public record PayTouristOspiteDichiaratoDto(string? NomeCompleto, DateTime? DataNascita, DateTime? CheckIn);

/// <summary>
/// Ciò che PayTourist ha già registrato nel periodo interrogato, sui due piani su cui si può
/// riconoscere una prenotazione già dichiarata:
/// <list type="bullet">
/// <item><see cref="PartnerIdPrenotazioni"/> — le dichiarazioni partite da noi, riconoscibili dalla
/// chiave che ci siamo dati (vedi <see cref="PayTouristDtoBuilder.PartnerIdPrenotazione"/>).</item>
/// <item><see cref="Ospiti"/> — chiunque risulti dichiarato, compreso chi è entrato da un file di
/// Pubblica Sicurezza caricato a mano sul portale: quel tracciato non contiene il nostro partner_id,
/// quindi l'unico modo di riconoscere quelle persone è l'anagrafica (vedi
/// <see cref="PayTouristDtoBuilder.ChiaveOspite"/>).</item>
/// </list>
/// </summary>
/// <param name="Completo">
/// False se la lettura si è fermata al tetto di pagine previsto invece di arrivare in fondo: il
/// contenuto è parziale, quindi una prenotazione non trovata qui potrebbe comunque essere già
/// dichiarata. Chi lo riceve deve dirlo nel log e non trattarlo come un "non c'è".
/// </param>
public record PayTouristDichiarazioniEsistenti(
    IReadOnlyList<string> PartnerIdPrenotazioni,
    IReadOnlyList<PayTouristOspiteDichiaratoDto> Ospiti,
    bool Completo = true)
{
    public static PayTouristDichiarazioniEsistenti Vuoto { get; } = new([], []);
}

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

    /// <summary>
    /// Cosa risulta già dichiarato su PayTourist con check-in nell'intervallo indicato
    /// (GET api/v1/reservations, paginato). Serve a non dichiarare due volte la stessa prenotazione:
    /// a differenza di una schedina alloggiati duplicata — sgradevole ma innocua — qui un doppione è
    /// imposta di soggiorno chiesta due volte allo stesso ospite, e l'Api non espone nessun modo per
    /// annullarla.
    /// </summary>
    /// <param name="dateCheckIn">
    /// I giorni di arrivo delle prenotazioni da controllare. Il client decide come interrogarli: per
    /// poche date fa una richiesta mirata per ciascuna, per molte una finestra unica paginata — la
    /// differenza conta quando la struttura ha molti arrivi, perché una finestra larga si porta
    /// dietro anche tutte le prenotazioni che non stiamo controllando.
    /// </param>
    Task<(bool Ok, PayTouristDichiarazioniEsistenti Dichiarazioni, string? Errore)> GetDichiarazioniEsistentiAsync(
        string token, string? comuneAttivita, int idStruttura, int idSoftware, IReadOnlyCollection<DateTime> dateCheckIn, CancellationToken cancellationToken);

    /// <summary>Elenco delle strutture abilitate su PayTourist per questo Token — usato per farle scegliere all'operatore invece di dover digitare a mano lo structure_id (vedi PayTouristConfigService.ListaStruttureDisponibiliAsync).</summary>
    Task<(bool Ok, IReadOnlyList<PayTouristStrutturaRemotaDto> Strutture, string? Errore)> GetStruttureAsync(string token, string? comuneAttivita, CancellationToken cancellationToken);

    /// <summary>Serializza una prenotazione nello stesso JSON che verrebbe inviato (campo "data" del multipart) — usato solo per l'export on-demand (mai inviato né persistito), garantisce che l'export mostri esattamente ciò che l'invio reale spedirebbe.</summary>
    string SerializzaPerExport(int idStruttura, int idSoftware, PayTouristReservationDto prenotazione);
}
