using System.Globalization;
using System.Text;
using System.Xml.Linq;
using GestiSoft.Application.Exceptions;
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
            token, LcodeInt(lcode), request.Woodoo ? 1 : 0, request.Nome, request.Occupancy, (int)request.PrezzoBase, request.Disponibilita, request.ShortName, request.Board);

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
        // get_channels_info, a differenza di tutte le altre chiamate, non risponde con l'array
        // [responseCode, dati] ma con uno struct diretto (confermato dalla documentazione ufficiale
        // Wubook) — usa un parsing dedicato, non VerificaEsito/InvocaAsync.
        var s = await InvocaStructAsync("get_channels_info", "il recupero dei canali", cancellationToken, token);
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

    public async Task<WubookCamera?> FetchSingleRoomAsync(string token, string lcode, int idCameraWubook, CancellationToken cancellationToken)
    {
        var (codice, dati, fault) = await InvocaAsync("fetch_single_room", cancellationToken, token, LcodeInt(lcode), idCameraWubook);
        if (codice != 0)
        {
            return null;
        }

        var s = dati?.Element("struct");
        return s is null
            ? null
            : new WubookCamera(
                Id: MembroInt(s, "id"),
                Nome: MembroStringa(s, "name") ?? string.Empty,
                ShortName: MembroStringa(s, "shortname"),
                Occupancy: MembroInt(s, "occupancy"),
                Prezzo: MembroDecimal(s, "price"),
                Disponibilita: MembroInt(s, "avail"),
                Subroom: MembroInt(s, "subroom"),
                Board: MembroStringa(s, "board"));
    }

    // --- Piani prezzo nominati (add_vplan/mod_vplans/del_plan/get_pricing_plans/update_plan_name) ---

    public async Task<IReadOnlyList<WubookPianoPrezzo>> GetPricingPlansAsync(string token, string lcode, CancellationToken cancellationToken)
    {
        var (codice, dati, fault) = await InvocaAsync("get_pricing_plans", cancellationToken, token, LcodeInt(lcode));
        VerificaEsito(codice, fault, "il recupero dei piani prezzo");

        return ElementiArray(dati)
            .Select(v => v.Element("struct"))
            .Where(s => s is not null)
            .Select(s => LeggiPianoPrezzo(s!))
            .ToList();
    }

    private static WubookPianoPrezzo LeggiPianoPrezzo(XElement s)
    {
        var haVpid = s.Elements("member").Any(m => m.Element("name")?.Value == "vpid");
        return new WubookPianoPrezzo(
            Id: MembroInt(s, "id"),
            Nome: MembroStringa(s, "name") ?? string.Empty,
            Daily: MembroBool(s, "daily"),
            IsVirtual: haVpid,
            ParentId: haVpid ? MembroInt(s, "vpid") : null,
            Variazione: haVpid ? MembroDecimal(s, "variation") : null,
            TipoVariazione: haVpid ? MembroInt(s, "variation_type") : null);
    }

    public async Task<int> AddVirtualPlanAsync(string token, string lcode, string nome, int parentId, int tipoVariazione, decimal variazione, CancellationToken cancellationToken)
    {
        var (codice, dati, fault) = await InvocaAsync(
            "add_vplan", cancellationToken, token, LcodeInt(lcode), nome, parentId, tipoVariazione, (double)variazione);
        VerificaEsito(codice, fault, "la creazione del piano prezzo");
        return PrimoIntero(dati) ?? throw new InvalidOperationException("Wubook non ha restituito un id piano prezzo valido.");
    }

    public async Task ModVirtualPlanAsync(string token, string lcode, int pianoId, int tipoVariazione, decimal variazione, CancellationToken cancellationToken)
    {
        var voce = StructOf([("pid", pianoId), ("variation", (double)variazione), ("variation_type", tipoVariazione)]);
        var (codice, _, fault) = await InvocaAsync("mod_vplans", cancellationToken, token, LcodeInt(lcode), ArrayOf([voce]));
        VerificaEsito(codice, fault, "l'aggiornamento del piano prezzo");
    }

    public async Task DelPlanAsync(string token, string lcode, int pianoId, CancellationToken cancellationToken)
    {
        var (codice, _, fault) = await InvocaAsync("del_plan", cancellationToken, token, LcodeInt(lcode), pianoId);
        VerificaEsito(codice, fault, "la rimozione del piano prezzo");
    }

    public async Task UpdatePlanNameAsync(string token, string lcode, int pianoId, string nome, CancellationToken cancellationToken)
    {
        var (codice, _, fault) = await InvocaAsync("update_plan_name", cancellationToken, token, LcodeInt(lcode), pianoId, nome);
        VerificaEsito(codice, fault, "la rinomina del piano prezzo");
    }

    // --- Piani restrizione nominati (rplan_add_rplan/rplan_rplans/rplan_rename_rplan/rplan_del_rplan/rplan_update_rplan_rules) ---

    public async Task<IReadOnlyList<WubookPianoRestrizione>> GetRestrictionPlansAsync(string token, string lcode, CancellationToken cancellationToken)
    {
        var (codice, dati, fault) = await InvocaAsync("rplan_rplans", cancellationToken, token, LcodeInt(lcode));
        VerificaEsito(codice, fault, "il recupero dei piani restrizione");

        return ElementiArray(dati)
            .Select(v => v.Element("struct"))
            .Where(s => s is not null)
            .Select(s => new WubookPianoRestrizione(MembroInt(s!, "id"), MembroStringa(s!, "name") ?? string.Empty, LeggiRegole(MembroStruct(s!, "rules"))))
            .ToList();
    }

    private static XElement? MembroStruct(XElement structEl, string nome) =>
        structEl.Elements("member").FirstOrDefault(m => m.Element("name")?.Value == nome)?.Element("value")?.Element("struct");

    private static WubookRegoleRestrizione? LeggiRegole(XElement? s)
    {
        if (s is null)
        {
            return null;
        }

        return new WubookRegoleRestrizione(
            MinStay: MembroIntOpt(s, "min_stay"),
            MinStayArrival: MembroIntOpt(s, "min_stay_arrival"),
            MaxStay: MembroIntOpt(s, "max_stay"),
            MaxStayArrival: MembroIntOpt(s, "max_stay_arrival"),
            Chiuso: MembroBoolOpt(s, "closed"),
            ChiusoArrivo: MembroBoolOpt(s, "closed_arrival"),
            ChiusoPartenza: MembroBoolOpt(s, "closed_departure"));
    }

    public async Task<int> AddRestrictionPlanAsync(string token, string lcode, string nome, CancellationToken cancellationToken)
    {
        var (codice, dati, fault) = await InvocaAsync("rplan_add_rplan", cancellationToken, token, LcodeInt(lcode), nome, 1);
        VerificaEsito(codice, fault, "la creazione del piano restrizione");
        return PrimoIntero(dati) ?? throw new InvalidOperationException("Wubook non ha restituito un id piano restrizione valido.");
    }

    public async Task RenameRestrictionPlanAsync(string token, string lcode, int pianoId, string nome, CancellationToken cancellationToken)
    {
        var (codice, _, fault) = await InvocaAsync("rplan_rename_rplan", cancellationToken, token, LcodeInt(lcode), pianoId, nome);
        VerificaEsito(codice, fault, "la rinomina del piano restrizione");
    }

    public async Task DelRestrictionPlanAsync(string token, string lcode, int pianoId, CancellationToken cancellationToken)
    {
        var (codice, _, fault) = await InvocaAsync("rplan_del_rplan", cancellationToken, token, LcodeInt(lcode), pianoId);
        VerificaEsito(codice, fault, "la rimozione del piano restrizione");
    }

    public async Task UpdateRestrictionPlanRulesAsync(string token, string lcode, int pianoId, WubookRegoleRestrizione regole, CancellationToken cancellationToken)
    {
        var membri = new List<(string, object)>();
        if (regole.MinStay is { } minStay) membri.Add(("min_stay", minStay));
        if (regole.MinStayArrival is { } minStayArrival) membri.Add(("min_stay_arrival", minStayArrival));
        if (regole.MaxStay is { } maxStay) membri.Add(("max_stay", maxStay));
        if (regole.MaxStayArrival is { } maxStayArrival) membri.Add(("max_stay_arrival", maxStayArrival));
        if (regole.Chiuso is { } chiuso) membri.Add(("closed", chiuso ? 1 : 0));
        if (regole.ChiusoArrivo is { } chiusoArrivo) membri.Add(("closed_arrival", chiusoArrivo ? 1 : 0));
        if (regole.ChiusoPartenza is { } chiusoPartenza) membri.Add(("closed_departure", chiusoPartenza ? 1 : 0));

        var (codice, _, fault) = await InvocaAsync("rplan_update_rplan_rules", cancellationToken, token, LcodeInt(lcode), pianoId, StructOf(membri));
        VerificaEsito(codice, fault, "l'aggiornamento delle regole del piano restrizione");
    }

    private static int? PrimoIntero(XElement? dati)
    {
        var testo = dati?.Element("int")?.Value ?? dati?.Descendants("int").FirstOrDefault()?.Value;
        return int.TryParse(testo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
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

        var documento = AnalizzaDocumento(corpo, metodo);

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

    /// <summary>
    /// Per le chiamate la cui risposta è direttamente uno struct (oggi solo get_channels_info) —
    /// non il consueto array [responseCode, dati] usato da tutte le altre chiamate Wubook, quindi
    /// nessun controllo di codice d'esito da fare (la documentazione ufficiale Wubook per questo
    /// metodo non prevede un codice d'errore separato).
    /// </summary>
    private async Task<XElement?> InvocaStructAsync(string metodo, string operazione, CancellationToken cancellationToken, params object[] parametri)
    {
        var richiesta = MethodCall(metodo, parametri);
        using var content = new StringContent(new XDocument(richiesta).ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        using var response = await http.PostAsync(Endpoint, content, cancellationToken);
        var corpo = await response.Content.ReadAsStringAsync(cancellationToken);

        var documento = AnalizzaDocumento(corpo, metodo);

        var fault = documento.Descendants("fault").FirstOrDefault();
        if (fault is not null)
        {
            var faultStruct = fault.Descendants("struct").FirstOrDefault();
            var messaggio = faultStruct is null ? null : MembroStringa(faultStruct, "faultString");
            throw new ConflictException($"Wubook ha rifiutato {operazione}: {messaggio ?? "errore sconosciuto"}.");
        }

        return documento.Descendants("param").FirstOrDefault()?.Element("value")?.Element("struct");
    }

    private static XDocument AnalizzaDocumento(string corpo, string metodo)
    {
        try
        {
            return XDocument.Parse(corpo);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Risposta Wubook non è XML valido per '{metodo}'.", ex);
        }
    }

    private static void VerificaEsito(int codice, string? fault, string operazione)
    {
        if (codice != 0)
        {
            throw new ConflictException($"Wubook ha rifiutato {operazione} (codice {codice}): {fault ?? "nessun dettaglio"}.");
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

    private static bool MembroBool(XElement structEl, string nome)
    {
        var valore = structEl.Elements("member").FirstOrDefault(m => m.Element("name")?.Value == nome)?.Element("value");
        var testo = valore?.Element("boolean")?.Value ?? valore?.Element("int")?.Value ?? valore?.Value;
        return testo == "1" || string.Equals(testo, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int? MembroIntOpt(XElement structEl, string nome) =>
        structEl.Elements("member").Any(m => m.Element("name")?.Value == nome) ? MembroInt(structEl, nome) : null;

    private static bool? MembroBoolOpt(XElement structEl, string nome) =>
        structEl.Elements("member").Any(m => m.Element("name")?.Value == nome) ? MembroBool(structEl, nome) : null;

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
