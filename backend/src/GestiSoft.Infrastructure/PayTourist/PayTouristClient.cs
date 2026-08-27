using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GestiSoft.Application.PayTourist;

namespace GestiSoft.Infrastructure.PayTourist;

/// <summary>
/// Client REST/JSON verso PayTourist — porta la region "PayTourist" di StatePoliceApiRepository del
/// legacy: POST api/v1/reservations (corpo <c>multipart/form-data</c>, un campo "data" contenente il
/// JSON — non JSON diretto, fedele all'API reale), GET api/v1/reductions (risposta <c>{"data": [...]}</c>),
/// GET api/v1/online-portals-enabled (risposta un array JSON diretto). Autenticazione con un bearer
/// token statico per Struttura, nessun login/refresh (a differenza di Osservatorio Turistico).
/// A differenza del legacy — che lasciava propagare un'eccezione (EnsureSuccessStatusCode) su
/// reductions/online-portals — ogni fallimento di trasporto o risposta non 2xx viene qui intercettato
/// e tradotto in un esito "non ok", mai un'eccezione grezza: stesso principio già applicato a tutti
/// gli altri client di questo progetto (Wubook/Alloggiati Web/Osservatorio).
/// </summary>
public class PayTouristClient(HttpClient http) : IPayTouristClient
{
    public async Task<PayTouristEsitoOperazione> InviaPrenotazioneAsync(string token, int idStruttura, int idSoftware, PayTouristReservationDto prenotazione, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(CostruisciWire(idStruttura, idSoftware, prenotazione));

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/reservations");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var content = new MultipartFormDataContent
            {
                { new StringContent(json, Encoding.UTF8, "application/json"), "data" },
            };
            request.Content = content;

            using var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new PayTouristEsitoOperazione(true, null);
            }

            var corpo = await response.Content.ReadAsStringAsync(cancellationToken);
            return new PayTouristEsitoOperazione(false, EstraiMessaggioErrore(corpo) ?? $"Richiesta rifiutata (stato: {response.StatusCode}).");
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return new PayTouristEsitoOperazione(false, "Servizio PayTourist non raggiungibile.");
        }
    }

    public async Task<(bool Ok, IReadOnlyList<PayTouristRiduzioneDto> Riduzioni, string? Errore)> GetRiduzioniAsync(string token, int idStruttura, int idSoftware, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreaRichiesta($"api/v1/reductions?structure_id={idStruttura}&software_id={idSoftware}", token);
            using var response = await http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var corpo = await response.Content.ReadAsStringAsync(cancellationToken);
                return (false, [], EstraiMessaggioErrore(corpo) ?? $"Richiesta riduzioni rifiutata (stato: {response.StatusCode}).");
            }

            var risultato = await response.Content.ReadFromJsonAsync<WireReductionResponse>(cancellationToken: cancellationToken);
            var riduzioni = (risultato?.Data ?? []).Select(r => new PayTouristRiduzioneDto(r.Id, r.Name)).ToList();
            return (true, riduzioni, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            return (false, [], "Servizio PayTourist non raggiungibile (riduzioni).");
        }
    }

    public async Task<(bool Ok, IReadOnlyList<PayTouristPortaleDto> Portali, string? Errore)> GetPortaliOnlineAsync(string token, int idStruttura, int idSoftware, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreaRichiesta($"api/v1/online-portals-enabled?structure_id={idStruttura}&software_id={idSoftware}", token);
            using var response = await http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var corpo = await response.Content.ReadAsStringAsync(cancellationToken);
                return (false, [], EstraiMessaggioErrore(corpo) ?? $"Richiesta portali online rifiutata (stato: {response.StatusCode}).");
            }

            var risultato = await response.Content.ReadFromJsonAsync<List<WirePortale>>(cancellationToken: cancellationToken) ?? [];
            return (true, risultato.Select(p => new PayTouristPortaleDto(p.Id, p.Name)).ToList(), null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            return (false, [], "Servizio PayTourist non raggiungibile (portali online).");
        }
    }

    public string SerializzaPerExport(int idStruttura, int idSoftware, PayTouristReservationDto prenotazione) =>
        JsonSerializer.Serialize(CostruisciWireReservation(prenotazione), new JsonSerializerOptions { WriteIndented = true });

    private static HttpRequestMessage CreaRichiesta(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static WirePayTourist CostruisciWire(int idStruttura, int idSoftware, PayTouristReservationDto prenotazione) =>
        new(idStruttura, idSoftware, [CostruisciWireReservation(prenotazione)]);

    private static WireReservation CostruisciWireReservation(PayTouristReservationDto p) => new(
        p.PartnerId,
        p.CheckIn.ToString("yyyy-MM-dd"),
        p.CheckOut.ToString("yyyy-MM-dd"),
        AmountPaid: 0,
        SendCopyByEmailToMasterGuest: 1,
        p.OnlinePortalId,
        p.TotalFromOnlinePortal,
        p.OnlinePortalReservationId,
        p.Guests.Select(CostruisciWireGuest).ToList());

    private static WireGuest CostruisciWireGuest(PayTouristGuestDto g) => new(
        g.PartnerId,
        PartnerRoomId: null,
        g.Nome ?? string.Empty,
        g.Cognome ?? string.Empty,
        g.Email ?? string.Empty,
        g.CheckIn.ToString("yyyy-MM-dd"),
        g.CheckOut.ToString("yyyy-MM-dd"),
        g.TypeId,
        g.DocumentType,
        g.DocumentNumber ?? string.Empty,
        g.DocumentReleasedByCountry,
        g.DocumentReleasedByCity,
        g.Sex,
        g.Nationality,
        g.ResidenceCountry,
        g.ResidenceCity,
        g.DateOfBirth?.ToString("yyyy-MM-dd"),
        g.BirthCountry,
        g.BirthCity,
        TaxRefused: 0,
        g.ReductionId,
        VehiclePlate: null);

    /// <summary>Porta il parsing best-effort del legacy: prova "message"/"errors" nel corpo JSON, altrimenti il testo grezzo.</summary>
    private static string? EstraiMessaggioErrore(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(corpo);
            var messaggio = doc.RootElement.TryGetProperty("message", out var messageEl) ? messageEl.GetString() : null;
            var dettagli = doc.RootElement.TryGetProperty("errors", out var errorsEl) ? errorsEl.ToString() : null;

            return dettagli is null ? messaggio : $"{messaggio} ({dettagli})".Trim();
        }
        catch (JsonException)
        {
            return corpo;
        }
    }

    private record WirePayTourist(
        [property: JsonPropertyName("structure_id")] int StructureId,
        [property: JsonPropertyName("software_id")] int SoftwareId,
        [property: JsonPropertyName("reservations")] IReadOnlyList<WireReservation> Reservations);

    private record WireReservation(
        [property: JsonPropertyName("partner_id")] string PartnerId,
        [property: JsonPropertyName("check_in_date")] string CheckInDate,
        [property: JsonPropertyName("check_out_date")] string CheckOutDate,
        [property: JsonPropertyName("amount_paid")] decimal AmountPaid,
        [property: JsonPropertyName("send_copy_by_email_to_master_guest")] int SendCopyByEmailToMasterGuest,
        [property: JsonPropertyName("online_portal")] int? OnlinePortal,
        [property: JsonPropertyName("total_from_online_portal")] decimal? TotalFromOnlinePortal,
        [property: JsonPropertyName("online_portal_reservation_id")] string? OnlinePortalReservationId,
        [property: JsonPropertyName("guests")] IReadOnlyList<WireGuest> Guests);

    private record WireGuest(
        [property: JsonPropertyName("partner_id")] string PartnerId,
        [property: JsonPropertyName("partner_room_id")] string? PartnerRoomId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("surname")] string Surname,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("check_in_date")] string CheckInDate,
        [property: JsonPropertyName("check_out_date")] string CheckOutDate,
        [property: JsonPropertyName("type_id")] int TypeId,
        [property: JsonPropertyName("document_type")] int? DocumentType,
        [property: JsonPropertyName("document_number")] string DocumentNumber,
        [property: JsonPropertyName("document_released_by_country")] int DocumentReleasedByCountry,
        [property: JsonPropertyName("document_released_by_city")] int DocumentReleasedByCity,
        [property: JsonPropertyName("sex")] int Sex,
        [property: JsonPropertyName("nationality")] int Nationality,
        [property: JsonPropertyName("residence_country")] int ResidenceCountry,
        [property: JsonPropertyName("residence_city")] int ResidenceCity,
        [property: JsonPropertyName("date_of_birth")] string? DateOfBirth,
        [property: JsonPropertyName("birth_country")] int BirthCountry,
        [property: JsonPropertyName("birth_city")] int BirthCity,
        [property: JsonPropertyName("tax_refused")] int TaxRefused,
        [property: JsonPropertyName("reduction_id")] int? ReductionId,
        [property: JsonPropertyName("vehicle_plate")] string? VehiclePlate);

    private record WireReductionResponse([property: JsonPropertyName("data")] IReadOnlyList<WireReduction> Data);

    private record WireReduction(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name);

    private record WirePortale(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name);
}
