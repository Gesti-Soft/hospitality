namespace GestiSoft.Application.Wubook;

public record WubookCamera(int Id, string Nome, string? ShortName, int Occupancy, decimal Prezzo, int Disponibilita, int Subroom, string? Board);

/// <summary>Valori di un singolo giorno per una camera Wubook, da fetch_rooms_values — il "avail" qui è quello reale del calendario giorno-per-giorno, non lo statico (e spesso inattendibile) campo "avail" di fetch_rooms.</summary>
public record WubookDisponibilitaGiorno(int Avail, bool Prenotata, bool Chiusa);

public record WubookNuovaCameraRequest(string Nome, string ShortName, int Occupancy, decimal PrezzoBase, int Disponibilita, string Board, bool Woodoo = false);

public record WubookPrenotazione(
    int RCode,
    string? ChannelReservationCode,
    string CameraIdWubookRaw,
    DateTime CheckIn,
    DateTime CheckOut,
    decimal Importo,
    int Adulti,
    int Bambini,
    int Status,
    int IdChannel,
    string? CustomerName,
    string? CustomerSurname,
    string? CustomerEmail,
    string? CustomerCountry,
    string? CustomerCity,
    DatiExtraOta? Extra = null,
    // Una per camera dell'ordine, nell'ordine in cui l'OTA le elenca (vedi OrdineOta).
    IReadOnlyList<CameraOrdineOta>? Camere = null);

/// <summary>
/// Una camera di un ordine OTA: id della camera OTA, somma dei prezzi per notte (`booked_rooms`, 0 se
/// l'OTA non li manda) e ospiti (`rooms_occupancies`, null se non indicati).
/// </summary>
public record CameraOrdineOta(int IdCameraWubook, decimal PrezzoNotti, int? Occupazione);

/// <summary>
/// Quello che una prenotazione OTA porta oltre ai dati standard, così come arriva: trattamento per
/// camera (`boards`: bb, hb, fb, ai, nb — lo compila solo il booking engine dell'OTA), extra
/// acquistati (`addons_list`), informazioni non standard del portale (`ancillary`, a coppie
/// chiave/valore appiattite) e richieste dell'ospite (`customer_notes`). Vedi TrattamentoOta.
/// </summary>
public record DatiExtraOta(
    IReadOnlyList<string> Boards,
    IReadOnlyList<ExtraOta> Extra,
    IReadOnlyList<KeyValuePair<string, string>> Ancillary,
    string? NoteCliente);

public record ExtraOta(string Nome, int Quantita, decimal Prezzo);

public record WubookCanale(int Id, string Nome);

public record WubookPianoPrezzo(int Id, string Nome, bool Daily, bool IsVirtual, int? ParentId, decimal? Variazione, int? TipoVariazione);

public record WubookRegoleRestrizione(int? MinStay, int? MinStayArrival, int? MaxStay, int? MaxStayArrival, bool? Chiuso, bool? ChiusoArrivo, bool? ChiusoPartenza);

public record WubookPianoRestrizione(int Id, string Nome, WubookRegoleRestrizione? Regole);

/// <summary>
/// Client XML-RPC verso Wubook (https://wired.wubook.net/xrws/) — porta OtaServiceApiRepository
/// del sistema legacy. token/lcode sono SEMPRE quelli ottenuti da IGestisoftLicenzaClient, mai
/// configurati localmente (vedi WubookLicenzaService). I nomi dei campi struct XML-RPC seguono
/// l'API pubblica documentata di Wubook (id/name/shortname/price/avail/board/occupancy/subroom);
/// NON è stato possibile verificarli contro un account Wubook reale in questa sessione — da
/// validare in sandbox prima dell'uso in produzione (vedi piano, sezione Verifica).
/// </summary>
public interface IWubookClient
{
    Task<IReadOnlyList<WubookCamera>> FetchRoomsAsync(string token, string lcode, CancellationToken cancellationToken);

    /// <summary>fetch_rooms_values — disponibilità/prenotato/chiuso reali per camera Wubook e giorno, nel periodo indicato (mai vuoto: dataInizio..dataFine inclusi). Se <paramref name="idCamereWubook"/> è null, Wubook risponde per tutte le camere della struttura.</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<WubookDisponibilitaGiorno>>> FetchDisponibilitaAsync(
        string token, string lcode, DateTime dataInizio, DateTime dataFine, IReadOnlyList<int>? idCamereWubook, CancellationToken cancellationToken);

    Task<int> NewRoomAsync(string token, string lcode, WubookNuovaCameraRequest request, CancellationToken cancellationToken);

    Task ModRoomAsync(string token, string lcode, int idCameraWubook, WubookNuovaCameraRequest request, CancellationToken cancellationToken);

    Task DelRoomAsync(string token, string lcode, int idCameraWubook, CancellationToken cancellationToken);

    /// <summary>Un prezzo per camera valido per tutti i giorni di [dataInizio, dataInizio+giorni.Count).</summary>
    Task UpdatePlanPricesAsync(string token, string lcode, DateTime dataInizio, IReadOnlyDictionary<int, IReadOnlyList<decimal>> prezziPerCamera, CancellationToken cancellationToken);

    /// <summary>0 = non disponibile, 1 = disponibile, per ogni camera/giorno.</summary>
    Task UpdateAvailabilityAsync(string token, string lcode, DateTime dataInizio, IReadOnlyDictionary<int, IReadOnlyList<int>> disponibilitaPerCamera, CancellationToken cancellationToken);

    /// <summary>Soggiorno minimo/massimo per ogni camera/giorno (piano restrizioni di default, pid=0 — fedele al legacy).</summary>
    Task UpdateRestrizioniAsync(string token, string lcode, DateTime dataInizio, IReadOnlyDictionary<int, IReadOnlyList<(int? MinStay, int? MaxStay)>> restrizioniPerCamera, CancellationToken cancellationToken);

    /// <summary>fetch_new_bookings con mark=1 — a differenza del legacy, qui l'array di risposta viene letto per intero (bug noto del legacy: leggeva solo la prima prenotazione, vedi report Fase 5).</summary>
    Task<IReadOnlyList<WubookPrenotazione>> FetchNewBookingsAsync(string token, string lcode, CancellationToken cancellationToken);

    Task<WubookPrenotazione?> FetchBookingAsync(string token, string lcode, int rcode, CancellationToken cancellationToken);

    /// <summary>
    /// fetch_bookings per data di creazione (oncreated=1), estremi inclusi: a differenza di
    /// fetch_new_bookings non segna nulla come letto. Massimo 120 prenotazioni per chiamata e 288
    /// chiamate ogni 12 ore (tdocs.wubook.net/wired/fetch.html, policies.html).
    /// </summary>
    Task<IReadOnlyList<WubookPrenotazione>> FetchBookingsCreateAsync(string token, string lcode, DateTime dal, DateTime al, CancellationToken cancellationToken);

    /// <summary>push_activation: indirizzo a cui l'OTA manda gli avvisi delle prenotazioni di questa struttura ("" = disattivati). Con <paramref name="prova"/> l'OTA manda subito un avviso di prova.</summary>
    Task PushActivationAsync(string token, string lcode, string url, bool prova, CancellationToken cancellationToken);

    /// <summary>push_url: indirizzo degli avvisi attualmente registrato per questa struttura, null se nessuno.</summary>
    Task<string?> PushUrlAsync(string token, string lcode, CancellationToken cancellationToken);

    Task<IReadOnlyList<WubookCanale>> GetChannelsInfoAsync(string token, CancellationToken cancellationToken);

    /// <summary>fetch_single_room — usata per risolvere il "subroom" (camera virtuale/pool) di una camera prima di chiusure/restrizioni per periodo, fedele a RoomsController.ResolveTargetRoom del legacy.</summary>
    Task<WubookCamera?> FetchSingleRoomAsync(string token, string lcode, int idCameraWubook, CancellationToken cancellationToken);

    // --- Piani prezzo nominati (PricingPlansController del legacy) ---

    Task<IReadOnlyList<WubookPianoPrezzo>> GetPricingPlansAsync(string token, string lcode, CancellationToken cancellationToken);

    Task<int> AddVirtualPlanAsync(string token, string lcode, string nome, int parentId, int tipoVariazione, decimal variazione, CancellationToken cancellationToken);

    Task ModVirtualPlanAsync(string token, string lcode, int pianoId, int tipoVariazione, decimal variazione, CancellationToken cancellationToken);

    Task DelPlanAsync(string token, string lcode, int pianoId, CancellationToken cancellationToken);

    Task UpdatePlanNameAsync(string token, string lcode, int pianoId, string nome, CancellationToken cancellationToken);

    // --- Piani restrizione nominati (RestrictionsController del legacy) ---

    Task<IReadOnlyList<WubookPianoRestrizione>> GetRestrictionPlansAsync(string token, string lcode, CancellationToken cancellationToken);

    Task<int> AddRestrictionPlanAsync(string token, string lcode, string nome, CancellationToken cancellationToken);

    Task RenameRestrictionPlanAsync(string token, string lcode, int pianoId, string nome, CancellationToken cancellationToken);

    Task DelRestrictionPlanAsync(string token, string lcode, int pianoId, CancellationToken cancellationToken);

    Task UpdateRestrictionPlanRulesAsync(string token, string lcode, int pianoId, WubookRegoleRestrizione regole, CancellationToken cancellationToken);
}
