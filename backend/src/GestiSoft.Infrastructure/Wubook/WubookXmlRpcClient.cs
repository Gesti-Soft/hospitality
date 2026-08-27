using System.Globalization;
using System.Text;
using System.Xml.Linq;
using GestiSoft.Application.Wubook;

namespace GestiSoft.Infrastructure.Wubook;

/// <summary>
/// Client XML-RPC verso Wubook (https://wired.wubook.net/xrws/) — porta
/// OtaServiceApiRepository/XmlRpcParser del sistema legacy. Non usa una libreria XML-RPC generica:
/// costruisce/legge direttamente gli elementi XML necessari (stesso approccio già usato per l'XML
/// SDI in Fase 4), dato che il set di chiamate è piccolo e fisso. Ogni risposta Wubook è un array
/// [responseCode:int, dati] — 0 = OK, negativo = errore (v. MappaErrore).
/// </summary>
public class WubookXmlRpcClient(HttpClient http) : IWubookClient
{
    public async Task<IReadOnlyList<WubookCamera>> FetchRoomsAsync(string token, string lcode, CancellationToken cancellationToken)
    {
        var (codice, dati, fault) = await InvocaAsync("fetch_rooms", cancellationToken, token, LcodeInt(lcode));
        VerificaEsito(codice, fault, "il recupero delle camere");

        var camere = new List<WubookCamera>();
        foreach (var valore in ElementiArray(dati))
        {
            var s = valore.Element("struct");
            if (s is null)
            {
                continue;
            }

            camere.Add(new WubookCamera(
                Id: MembroInt(s, "id"),
                Nome: MembroStringa(s, "name") ?? string.Empty,
                ShortName: MembroStringa(s, "shortname"),
                Occupancy: MembroInt(s, "occupancy"),
                Prezzo: MembroDecimal(s, "price"),
                Disponibilita: MembroInt(s, "avail"),
                Subroom: MembroInt(s, "subroom"),
                Board: MembroStringa(s, "board")));
        }

        return camere;
    }

    public async Task<int> NewRoomAsync(string token, string lcode, WubookNuovaCameraRequest request, CancellationToken cancellationToken)
    {
        var (codice, dati, fault) = await InvocaAsync(
            "new_room", cancellationToken,
            token, LcodeInt(lcode), 0, request.Nome, request.Occupancy, (int)request.PrezzoBase, request.Disponibilita, request.ShortName, request.Board);

        VerificaEsito(codice, fault, "la creazione della camera");

        var id = dati?.Element("int")?.Value ?? dati?.Descendants("int").FirstOrDefault()?.Value;
        if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idCamera) || idCamera <= 0)
        {
            throw new InvalidOperationException("Wubook non ha restituito un id camera valido.");
        }

        return idCamera;
    }

    public async Task ModRoomAsync(string token, string lcode, int idCameraWubook, WubookNuovaCameraRequest request, CancellationToken cancellationToken)
    {
        var (codice, _, fault) = await InvocaAsync(
            "mod_room", cancellationToken,
            token, LcodeInt(lcode), idCameraWubook, request.Nome, request.Occupancy, (int)request.PrezzoBase, request.Disponibilita, request.ShortName, request.Board);

        VerificaEsito(codice, fault, "l'aggiornamento della camera");
    }

    public async Task DelRoomAsync(string token, string lcode, int idCameraWubook, CancellationToken cancellationToken)
    {
        var (codice, _, fault) = await InvocaAsync("del_room", cancellationToken, token, LcodeInt(lcode), idCameraWubook);
        VerificaEsito(codice, fault, "la rimozione della camera");
    }

    public async Task UpdatePlanPricesAsync(string token, string lcode, DateTime dataInizio, IReadOnlyDictionary<int, IReadOnlyList<decimal>> prezziPerCamera, CancellationToken cancellationToken)
    {
        var struttura = StructOf(prezziPerCamera.Select(kv => (
            kv.Key.ToString(CultureInfo.InvariantCulture),
            (object)ArrayOf(kv.Value.Select(p => (object)(double)p)))));

        var (codice, _, fault) = await InvocaAsync(
            "update_plan_prices", cancellationToken,
            token, LcodeInt(lcode), 0, dataInizio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), struttura);

        VerificaEsito(codice, fault, "l'aggiornamento dei prezzi");
    }

    public async Task UpdateAvailabilityAsync(string token, string lcode, DateTime dataInizio, IReadOnlyDictionary<int, IReadOnlyList<int>> disponibilitaPerCamera, CancellationToken cancellationToken)
    {
        var righe = disponibilitaPerCamera.Select(kv => (object)StructOf(
        [
            ("id", kv.Key),
            ("days", ArrayOf(kv.Value.Select(a => (object)StructOf([("avail", a)])))),
        ]));

        var (codice, _, fault) = await InvocaAsync(
            "update_avail", cancellationToken,
            token, LcodeInt(lcode), dataInizio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), ArrayOf(righe));

        VerificaEsito(codice, fault, "l'aggiornamento della disponibilità");
    }

    public async Task UpdateRestrizioniAsync(string token, string lcode, DateTime dataInizio, IReadOnlyDictionary<int, IReadOnlyList<(int? MinStay, int? MaxStay)>> restrizioniPerCamera, CancellationToken cancellationToken)
    {
        var struttura = StructOf(restrizioniPerCamera.Select(kv => (
            kv.Key.ToString(CultureInfo.InvariantCulture),
            (object)ArrayOf(kv.Value.Select(r =>
            {
                var membri = new List<(string, object)>();
                if (r.MinStay is { } min)
                {
                    membri.Add(("min_stay", min));
                }

                if (r.MaxStay is { } max)
                {
                    membri.Add(("max_stay", max));
                }

                return (object)StructOf(membri);
            })))));

        var (codice, _, fault) = await InvocaAsync(
            "rplan_update_rplan_values", cancellationToken,
            token, LcodeInt(lcode), 0, dataInizio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), struttura);

        VerificaEsito(codice, fault, "l'aggiornamento delle restrizioni");
    }

    public async Task<IReadOnlyList<WubookPrenotazione>> FetchNewBookingsAsync(string token, string lcode, CancellationToken cancellationToken)
    {
        var (codice, dati, fault) = await InvocaAsync("fetch_new_bookings", cancellationToken, token, LcodeInt(lcode), 1, 1);
        VerificaEsito(codice, fault, "il recupero delle nuove prenotazioni");

        return ElementiArray(dati)
            .Select(v => v.Element("struct"))
            .Where(s => s is not null)
            .Select(s => LeggiPrenotazione(s!))
            .ToList();
    }

    public async Task<WubookPrenotazione?> FetchBookingAsync(string token, string lcode, int rcode, CancellationToken cancellationToken)
    {
        var (codice, dati, fault) = await InvocaAsync("fetch_booking", cancellationToken, token, LcodeInt(lcode), rcode, 1);
        if (codice != 0)
        {
            return null;
        }

        var s = dati?.Element("struct") ?? ElementiArray(dati).Select(v => v.Element("struct")).FirstOrDefault(x => x is not null);
        return s is null ? null : LeggiPrenotazione(s);
    }

    public async Task<IReadOnlyList<WubookCanale>> GetChannelsInfoAsync(string token, CancellationToken cancellationToken)
    {
        var (codice, dati, fault) = await InvocaAsync("get_channels_info", cancellationToken, token);
        VerificaEsito(codice, fault, "il recupero dei canali");

        var s = dati?.Element("struct");
        if (s is null)
        {
            return Array.Empty<WubookCanale>();
        }

        var canali = new List<WubookCanale>();
        foreach (var membro in s.Elements("member"))
        {
            var idTesto = membro.Element("name")?.Value;
            if (!int.TryParse(idTesto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }

            var nome = membro.Element("value")?.Element("struct") is { } infoStruct ? MembroStringa(infoStruct, "name") : null;
            canali.Add(new WubookCanale(id, nome ?? $"Canale {id}"));
        }

        return canali;
    }

    private static WubookPrenotazione LeggiPrenotazione(XElement s) => new(
        RCode: MembroInt(s, "reservation_code") is var rc && rc != 0 ? rc : MembroInt(s, "id"),
        ChannelReservationCode: MembroStringa(s, "channel_reservation_code"),
        CameraIdWubookRaw: MembroStringa(s, "id_room") ?? MembroStringa(s, "rooms") ?? "0",
        CheckIn: MembroData(s, "date_arrival") ?? DateTime.UtcNow.Date,
        CheckOut: MembroData(s, "date_departure") ?? DateTime.UtcNow.Date,
        Importo: MembroDecimal(s, "amount"),
        Adulti: MembroInt(s, "men"),
        Bambini: MembroInt(s, "children"),
        Status: MembroInt(s, "status"),
        IdChannel: MembroInt(s, "id_channel"),
        CustomerName: MembroStringa(s, "customer_name"),
        CustomerSurname: MembroStringa(s, "customer_surname"),
        CustomerEmail: MembroStringa(s, "customer_mail") is { } mail && mail != "--" ? mail : null,
        CustomerCountry: MembroStringa(s, "customer_country"),
        CustomerCity: MembroStringa(s, "customer_city"));

    // --- Trasporto/parsing XML-RPC ---

    private const string Endpoint = "https://wired.wubook.net/xrws/";

    private async Task<(int Codice, XElement? Dati, string? Fault)> InvocaAsync(string metodo, CancellationToken cancellationToken, params object[] parametri)
    {
        var richiesta = MethodCall(metodo, parametri);
        using var content = new StringContent(new XDocument(richiesta).ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        using var response = await http.PostAsync(Endpoint, content, cancellationToken);
        var corpo = await response.Content.ReadAsStringAsync(cancellationToken);

        XDocument documento;
        try
        {
            documento = XDocument.Parse(corpo);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Risposta Wubook non è XML valido per '{metodo}'.", ex);
        }

        var fault = documento.Descendants("fault").FirstOrDefault();
        if (fault is not null)
        {
            var faultStruct = fault.Descendants("struct").FirstOrDefault();
            var messaggio = faultStruct is null ? null : MembroStringa(faultStruct, "faultString");
            return (-1, null, messaggio ?? "Errore Wubook sconosciuto");
        }

        var valoriTopLevel = documento.Descendants("data").FirstOrDefault()?.Elements("value").ToList();
        if (valoriTopLevel is null || valoriTopLevel.Count == 0)
        {
            return (-1, null, $"Risposta Wubook non valida per '{metodo}'.");
        }

        var codiceTesto = valoriTopLevel[0].Element("int")?.Value ?? valoriTopLevel[0].Element("i4")?.Value ?? valoriTopLevel[0].Value;
        var codice = int.TryParse(codiceTesto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) ? c : -1;
        var dati = valoriTopLevel.Count > 1 ? valoriTopLevel[1] : null;

        return (codice, dati, null);
    }

    private static void VerificaEsito(int codice, string? fault, string operazione)
    {
        if (codice != 0)
        {
            throw new InvalidOperationException($"Wubook ha rifiutato {operazione} (codice {codice}): {fault ?? "nessun dettaglio"}.");
        }
    }

    private static int LcodeInt(string lcode) =>
        int.TryParse(lcode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static IEnumerable<XElement> ElementiArray(XElement? valore) =>
        valore?.Element("array")?.Element("data")?.Elements("value") ?? Enumerable.Empty<XElement>();

    private static string? MembroStringa(XElement structEl, string nome)
    {
        var valore = structEl.Elements("member").FirstOrDefault(m => m.Element("name")?.Value == nome)?.Element("value");
        return valore?.Element("string")?.Value ?? valore?.Value;
    }

    private static int MembroInt(XElement structEl, string nome)
    {
        var valore = structEl.Elements("member").FirstOrDefault(m => m.Element("name")?.Value == nome)?.Element("value");
        var testo = valore?.Element("int")?.Value ?? valore?.Element("i4")?.Value ?? valore?.Value;
        return int.TryParse(testo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static decimal MembroDecimal(XElement structEl, string nome)
    {
        var valore = structEl.Elements("member").FirstOrDefault(m => m.Element("name")?.Value == nome)?.Element("value");
        var testo = valore?.Element("double")?.Value ?? valore?.Value;
        return decimal.TryParse(testo, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    private static DateTime? MembroData(XElement structEl, string nome)
    {
        var testo = MembroStringa(structEl, nome);
        if (string.IsNullOrWhiteSpace(testo))
        {
            return null;
        }

        return DateTime.TryParse(testo, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data.Date : null;
    }

    // --- Costruzione XML-RPC ---

    private static XElement MethodCall(string metodo, IEnumerable<object> parametri) => new(
        "methodCall",
        new XElement("methodName", metodo),
        new XElement("params", parametri.Select(p => new XElement("param", Valore(p)))));

    private static XElement Valore(object valore) => valore switch
    {
        int i => new XElement("value", new XElement("int", i)),
        double d => new XElement("value", new XElement("double", d.ToString(CultureInfo.InvariantCulture))),
        string s => new XElement("value", new XElement("string", s)),
        XElement gia => new XElement("value", gia),
        _ => throw new NotSupportedException($"Tipo XML-RPC non supportato: {valore.GetType()}"),
    };

    private static XElement ArrayOf(IEnumerable<object> elementi) =>
        new("array", new XElement("data", elementi.Select(Valore)));

    private static XElement StructOf(IEnumerable<(string Nome, object Valore)> membri) =>
        new("struct", membri.Select(m => new XElement("member", new XElement("name", m.Nome), Valore(m.Valore))));
}
