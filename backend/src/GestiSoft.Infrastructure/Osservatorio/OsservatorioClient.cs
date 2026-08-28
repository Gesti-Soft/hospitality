using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using GestiSoft.Application.Osservatorio;

namespace GestiSoft.Infrastructure.Osservatorio;

/// <summary>
/// Client REST/XML verso l'Osservatorio Turistico — porta la region "Osservatorio Turistico" di
/// StatePoliceApiRepository del legacy (login via header UserId/Password su GET auth/login,
/// enddayfrompms e stay/add-update via XML scritto a mano, stesso approccio delle altre
/// integrazioni di questo progetto invece di XmlSerializer). BaseAddress configurata via DI
/// (Osservatorio:BaseUrl). Aggiunto un retry breve sui soli fallimenti di trasporto (timeout,
/// connessione rifiutata) — il legacy non ne aveva nessuno — per assorbire un singolo blip di rete
/// senza dover aspettare il giro successivo del job giornaliero.
/// </summary>
public class OsservatorioClient(HttpClient http) : IOsservatorioClient
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    public async Task<OsservatorioLoginRisultato> LoginAsync(string entityCode, string password, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "auth/login");
        request.Headers.TryAddWithoutValidation("UserId", entityCode);
        request.Headers.TryAddWithoutValidation("Password", password);

        HttpResponseMessage? response;
        try
        {
            response = await InviaConRetryAsync(() => http.SendAsync(CloneRequest(request), cancellationToken), cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return new OsservatorioLoginRisultato(false, null, "Servizio Osservatorio Turistico non raggiungibile.");
        }

        if (response is null || !response.IsSuccessStatusCode)
        {
            return new OsservatorioLoginRisultato(false, null, $"Login rifiutato (stato: {response?.StatusCode.ToString() ?? "nessuna risposta"}).");
        }

        string? token = null;
        if (response.Headers.TryGetValues("Authorization", out var valori))
        {
            token = string.Join(",", valori);
        }
        else
        {
            token = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim('"');
        }

        token = token?.Replace("Bearer", string.Empty).Trim();

        return string.IsNullOrWhiteSpace(token)
            ? new OsservatorioLoginRisultato(false, null, "Token non presente nella risposta di login.")
            : new OsservatorioLoginRisultato(true, token, null);
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/logout");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            await http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            // Il logout è una cortesia verso il servizio esterno: un fallimento qui non deve
            // interrompere il batch, la sessione scadrà comunque lato server.
        }
    }

    public async Task<DateTime?> GetCurrentStatusDateAsync(string token, string hotelCode, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"entity/GetCurrentStatusDate/{hotelCode}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await InviaConRetryAsync(() => http.SendAsync(CloneRequest(request), cancellationToken), cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        // Risposta: una stringa JSON con virgolette contenente un istante UTC (es. "2026-04-30T22:00:00Z"),
        // che convertito in ora italiana (il container gira con TZ=Europe/Rome) dà la data "pulita"
        // a cui il server è fermo (nell'esempio, 1° maggio 2026 a mezzanotte locale) — è già il
        // PROSSIMO giorno da chiudere, non l'ultimo chiuso: verificato dal vivo, un +1 aggiuntivo
        // produceva "Invalid Date" (il server rifiuta di saltare un giorno).
        var corpo = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim().Trim('"');
        return DateTimeOffset.TryParse(corpo, CultureInfo.InvariantCulture, DateTimeStyles.None, out var istante)
            ? istante.ToLocalTime().Date
            : null;
    }

    public async Task<OsservatorioEsitoOperazione> EndDayAsync(string token, string hotelCode, DateTime data, CancellationToken cancellationToken)
    {
        // Il legacy girava su un server con orologio in ora italiana: "zzz"/"o" producevano sempre
        // l'offset italiano (+01:00/+02:00). "data" qui arriva con Kind=Utc (DateTime.UtcNow.Date):
        // formattarla così com'è produce sempre "+00:00" — va ritaggata come locale (il container
        // gira con TZ=Europe/Rome). Formato "o" (round-trip, secondi frazionari + offset) come
        // nell'esempio della pagina Help dell'endpoint reale.
        var dataLocale = DateTime.SpecifyKind(data.Date, DateTimeKind.Local);
        var payload = new XElement("EndDayPmsDTO",
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XElement("HotelCode", hotelCode),
            new XElement("CurrentDate", dataLocale.ToString("o", CultureInfo.InvariantCulture)));

        return await InviaOperazioneAsync("entity/enddayfrompms", token, payload, cancellationToken);
    }

    public Task<OsservatorioEsitoOperazione> SendArrivalsAsync(string token, string hotelCode, IReadOnlyList<OsservatorioStayDto> stays, CancellationToken cancellationToken) =>
        InviaOperazioneAsync("stay/addfrompms", token, CostruisciStaysPmsDto(stays), cancellationToken);

    public Task<OsservatorioEsitoOperazione> SendCheckoutsAsync(string token, string hotelCode, IReadOnlyList<OsservatorioStayDto> stays, CancellationToken cancellationToken) =>
        InviaOperazioneAsync("stay/updatefrompms", token, CostruisciStaysPmsDto(stays), cancellationToken);

    private async Task<OsservatorioEsitoOperazione> InviaOperazioneAsync(string percorso, string token, XElement payload, CancellationToken cancellationToken)
    {
        try
        {
            var testoXml = new XDocument(payload).ToString(SaveOptions.DisableFormatting);
            var response = await InviaConRetryAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, percorso)
                {
                    Content = new StringContent(testoXml, Encoding.UTF8, "application/xml"),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return http.SendAsync(request, cancellationToken);
            }, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new OsservatorioEsitoOperazione(true, null);
            }

            var corpo = await response.Content.ReadAsStringAsync(cancellationToken);
            return new OsservatorioEsitoOperazione(false, $"Richiesta rifiutata (stato: {response.StatusCode}). {corpo}".Trim());
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return new OsservatorioEsitoOperazione(false, "Servizio Osservatorio Turistico non raggiungibile.");
        }
    }

    private static XElement CostruisciStaysPmsDto(IReadOnlyList<OsservatorioStayDto> stays) =>
        new("StaysPmsDTO", stays.Select(CostruisciStay));

    private static XElement CostruisciStay(OsservatorioStayDto stay) =>
        new("Stay",
            new XElement("StayId", stay.StayId),
            new XElement("Guests", stay.Guests.Select(CostruisciGuest)));

    private static XElement CostruisciGuest(OsservatorioGuestDto guest) =>
        new("Guest",
            new XElement("GuestId", guest.GuestId),
            new XElement("Age", guest.Age),
            new XElement("NationalityCode", guest.NationalityCode),
            new XElement("BirthPlaceCode", guest.BirthPlaceCode),
            new XElement("ResidencePlaceCode", guest.ResidencePlaceCode),
            new XElement("Type", guest.Type),
            new XElement("Gender", guest.Gender),
            new XElement("EMail", guest.EMail ?? string.Empty),
            new XElement("ArrivalDate", FormattaData(guest.ArrivalDate)),
            new XElement("DepartureDate", FormattaData(guest.DepartureDate)),
            new XElement("Checkout", guest.Checkout ? "true" : "false"),
            new XElement("BedOccupancy", "true"),
            new XElement("Rooms", guest.Rooms.Select(CostruisciRoom)));

    private static XElement CostruisciRoom(OsservatorioRoomDto room) =>
        new("Room",
            new XElement("RoomId", room.RoomId),
            new XElement("StartDate", FormattaData(room.StartDate)),
            new XElement("EndDate", FormattaData(room.EndDate)));

    private static string FormattaData(DateTime data) => data.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    /// <summary>Fino a 2 tentativi aggiuntivi (3 totali) sui soli fallimenti di trasporto — mai su una risposta HTTP ricevuta con esito applicativo negativo, che non va ripetuta.</summary>
    private static async Task<HttpResponseMessage> InviaConRetryAsync(Func<Task<HttpResponseMessage>> invio, CancellationToken cancellationToken)
    {
        var ritardi = new[] { TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(900) };

        for (var tentativo = 0; ; tentativo++)
        {
            try
            {
                return await invio();
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && tentativo < ritardi.Length)
            {
                await Task.Delay(ritardi[tentativo], cancellationToken);
            }
        }
    }
}
