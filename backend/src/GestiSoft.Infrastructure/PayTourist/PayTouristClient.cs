using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GestiSoft.Application.PayTourist;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
/// A differenza degli altri client, l'host non è configurato una volta sola in DI: PayTourist assegna
/// un sottodominio per Comune (es. https://palermo.paytourist.com), quindi "PayTourist:BaseUrl" è un
/// template con il segnaposto "{comune}" (es. "https://{comune}.paytourist.com") risolto qui ad ogni
/// chiamata in base al Comune Attività della Struttura chiamante — su istruzione esplicita dell'utente.
/// </summary>
public class PayTouristClient(HttpClient http, IConfiguration configuration, ILogger<PayTouristClient> logger) : IPayTouristClient
{
    public async Task<PayTouristEsitoOperazione> InviaPrenotazioneAsync(string token, string? comuneAttivita, int idStruttura, int idSoftware, PayTouristReservationDto prenotazione, CancellationToken cancellationToken)
    {
        if (RisolviBaseUri(comuneAttivita) is not { } baseUri)
        {
            return new PayTouristEsitoOperazione(false, ErroreConfigurazione(comuneAttivita));
        }

        try
        {
            var json = JsonSerializer.Serialize(CostruisciWire(idStruttura, idSoftware, prenotazione), OpzioniJson);

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "api/v1/reservations"));
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
            logger.LogWarning(ex, "PayTourist InviaPrenotazioneAsync: fallimento di trasporto verso {BaseUri}", baseUri);
            return new PayTouristEsitoOperazione(false, "Servizio PayTourist non raggiungibile.");
        }
    }

    public async Task<(bool Ok, IReadOnlyList<PayTouristRiduzioneDto> Riduzioni, string? Errore)> GetRiduzioniAsync(string token, string? comuneAttivita, int idStruttura, int idSoftware, CancellationToken cancellationToken)
    {
        if (RisolviBaseUri(comuneAttivita) is not { } baseUri)
        {
            return (false, [], ErroreConfigurazione(comuneAttivita));
        }

        try
        {
            using var request = CreaRichiesta(baseUri, $"api/v1/reductions?structure_id={idStruttura}&software_id={idSoftware}", token);
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
            logger.LogWarning(ex, "PayTourist GetRiduzioniAsync: fallimento di trasporto verso {BaseUri}", baseUri);
            return (false, [], "Servizio PayTourist non raggiungibile (riduzioni).");
        }
    }

    public async Task<(bool Ok, IReadOnlyList<PayTouristPortaleDto> Portali, string? Errore)> GetPortaliOnlineAsync(string token, string? comuneAttivita, int idStruttura, int idSoftware, CancellationToken cancellationToken)
    {
        if (RisolviBaseUri(comuneAttivita) is not { } baseUri)
        {
            return (false, [], ErroreConfigurazione(comuneAttivita));
        }

        try
        {
            using var request = CreaRichiesta(baseUri, $"api/v1/online-portals-enabled?structure_id={idStruttura}&software_id={idSoftware}", token);
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
            logger.LogWarning(ex, "PayTourist GetPortaliOnlineAsync: fallimento di trasporto verso {BaseUri}", baseUri);
            return (false, [], "Servizio PayTourist non raggiungibile (portali online).");
        }
    }

    /// <summary>
    /// Elenco strutture abilitate su PayTourist per questo Token (GET api/v1/structures). La
    /// documentazione pubblica dell'API non mostra un esempio di risposta: qui si prova sia il
    /// formato "array diretto" (come online-portals-enabled) sia quello "wrappato in data" (come
    /// reductions), e per ogni elemento si cercano più nomi di campo plausibili per id/nome — se
    /// PayTourist usa nomi diversi da quelli previsti, l'elenco risulta vuoto invece di lanciare
    /// un'eccezione: l'operatore può sempre inserire l'Id struttura a mano come fallback.
    /// </summary>
    public async Task<(bool Ok, IReadOnlyList<PayTouristStrutturaRemotaDto> Strutture, string? Errore)> GetStruttureAsync(string token, string? comuneAttivita, CancellationToken cancellationToken)
    {
        if (RisolviBaseUri(comuneAttivita) is not { } baseUri)
        {
            return (false, [], ErroreConfigurazione(comuneAttivita));
        }

        try
        {
            using var request = CreaRichiesta(baseUri, "api/v1/structures", token);
            using var response = await http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var corpo = await response.Content.ReadAsStringAsync(cancellationToken);
                return (false, [], EstraiMessaggioErrore(corpo) ?? $"Richiesta strutture rifiutata (stato: {response.StatusCode}).");
            }

            var testo = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(testo);
            var elementi = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement
                : doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array ? dataEl : default;

            if (elementi.ValueKind != JsonValueKind.Array)
            {
                return (false, [], "Risposta strutture PayTourist non riconosciuta.");
            }

            var strutture = elementi.EnumerateArray()
                .Select(EstraiStruttura)
                .Where(s => s is not null)
                .Select(s => new PayTouristStrutturaRemotaDto(s!.Value.Id, s.Value.Nome))
                .ToList();

            return (true, strutture, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "PayTourist GetStruttureAsync: fallimento di trasporto verso {BaseUri}", baseUri);
            return (false, [], "Servizio PayTourist non raggiungibile (strutture).");
        }
    }

    private static (int Id, string Nome)? EstraiStruttura(JsonElement el)
    {
        if (!TryGetInt(el, out var id, "id", "structure_id"))
        {
            return null;
        }

        var nome = TryGetString(el, "name", "structure_name", "business_name", "denomination") ?? $"Struttura #{id}";
        return (id, nome);
    }

    private static bool TryGetInt(JsonElement el, out int value, params string[] chiavi)
    {
        foreach (var chiave in chiavi)
        {
            if (!el.TryGetProperty(chiave, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static string? TryGetString(JsonElement el, params string[] chiavi)
    {
        foreach (var chiave in chiavi)
        {
            if (el.TryGetProperty(chiave, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    public string SerializzaPerExport(int idStruttura, int idSoftware, PayTouristReservationDto prenotazione) =>
        JsonSerializer.Serialize(CostruisciWireReservation(prenotazione), new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

    /// <summary>
    /// PayTourist rifiuta un campo opzionale come "online_portal" quando è presente ma esplicitamente
    /// null (l'errore osservato dal vivo è "Portale online inesistente", segno che lo valida come un
    /// id anche quando è null invece di trattare l'assenza di valore come "nessun portale") — verificato
    /// contro l'ambiente di test dopo che una prenotazione senza portale corrispondente al canale
    /// veniva sempre respinta. Qui si omettono dal JSON tutti i campi null, non solo online_portal,
    /// per lo stesso motivo (document_type/date_of_birth/vehicle_plate/reduction_id sono ugualmente
    /// opzionali sul singolo ospite).
    /// </summary>
    private static readonly JsonSerializerOptions OpzioniJson = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private static HttpRequestMessage CreaRichiesta(Uri baseUri, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    /// <summary>
    /// Sostituisce "{comune}" nel template "PayTourist:BaseUrl" con lo slug del Comune Attività
    /// (Impostazioni Generali) della Struttura chiamante. Restituisce null se manca il template in
    /// configurazione o se il Comune Attività non è configurato/non produce uno slug utilizzabile —
    /// in entrambi i casi il chiamante deve trattarlo come "PayTourist non raggiungibile", mai
    /// lanciare un'eccezione grezza (stesso principio degli altri esiti di questo client).
    /// </summary>
    private Uri? RisolviBaseUri(string? comuneAttivita)
    {
        var template = configuration["PayTourist:BaseUrl"];
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var slug = SlugComune(comuneAttivita);
        if (string.IsNullOrEmpty(slug))
        {
            return null;
        }

        var url = template.Replace("{comune}", slug, StringComparison.OrdinalIgnoreCase);
        return Uri.TryCreate(url.TrimEnd('/') + "/", UriKind.Absolute, out var uri) ? uri : null;
    }

    private static string ErroreConfigurazione(string? comuneAttivita) =>
        string.IsNullOrEmpty(SlugComune(comuneAttivita))
            ? "Comune Attività non configurato in Impostazioni Generali: necessario per determinare l'host PayTourist."
            : "URL PayTourist non configurato (PayTourist:BaseUrl).";

    /// <summary>Comune Attività ("COMUNE (PROVINCIA)") ridotto a slug per il sottodominio PayTourist: minuscolo, senza spazi/accenti/apostrofi/punteggiatura.</summary>
    private static string SlugComune(string? comuneAttivita)
    {
        var citta = ExtractCity(comuneAttivita);
        if (string.IsNullOrWhiteSpace(citta))
        {
            return string.Empty;
        }

        var normalizzato = citta.Normalize(NormalizationForm.FormD);
        var senzaAccenti = new string(normalizzato.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return new string(senzaAccenti.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    /// <summary>I campi Comune sono salvati come "COMUNE (PROVINCIA)" — stesso ExtractCity già usato in PayTouristDtoBuilder/SchedinaAlloggiatiWebBuilder/StayBuilderOsservatorio, duplicato qui perché Infrastructure non deve dipendere da un metodo privato dell'Application.</summary>
    private static string? ExtractCity(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        var parts = input.Split(" (");
        return parts.Length > 2 ? $"{parts[0].Trim()} ({parts[1].Trim()}" : parts[0].Trim();
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

    /// <summary>
    /// Porta il parsing best-effort del legacy: prova "message"/"errors" nel corpo JSON, altrimenti
    /// il testo grezzo. Il corpo non è sempre un oggetto — su alcuni errori (verificato dal vivo
    /// contro l'ambiente di test) PayTourist risponde con un array JSON di stringhe invece che con
    /// {"message": ..., "errors": ...}: <c>JsonElement.TryGetProperty</c> lancia
    /// InvalidOperationException su un elemento che non è un oggetto, e quella prima veniva presa
    /// dal catch generico "servizio non raggiungibile" del chiamante, nascondendo il vero errore
    /// (bug reale, non ipotetico). Qui si controlla il tipo di elemento prima di interrogarlo.
    /// </summary>
    private static string? EstraiMessaggioErrore(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(corpo);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var voci = doc.RootElement.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString())
                    .Where(v => !string.IsNullOrWhiteSpace(v));
                var unito = string.Join(" | ", voci);
                return string.IsNullOrWhiteSpace(unito) ? corpo : unito;
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return corpo;
            }

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
