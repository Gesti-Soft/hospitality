namespace GestiSoft.Application.Wubook;

public record WubookCamera(int Id, string Nome, string? ShortName, int Occupancy, decimal Prezzo, int Disponibilita, int Subroom, string? Board);

public record WubookNuovaCameraRequest(string Nome, string ShortName, int Occupancy, decimal PrezzoBase, int Disponibilita, string Board);

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
    string? CustomerCity);

public record WubookCanale(int Id, string Nome);

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

    Task<IReadOnlyList<WubookCanale>> GetChannelsInfoAsync(string token, CancellationToken cancellationToken);
}
