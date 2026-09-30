using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Wubook;

/// <summary>
/// Trattamento letto da una prenotazione OTA. <see cref="Indicato"/> false = l'OTA non dice nulla
/// di riconoscibile, e il trattamento già sulla prenotazione non si tocca; true con
/// <see cref="Trattamento"/> null = l'OTA dice esplicitamente "solo pernottamento".
/// </summary>
public record TrattamentoOtaRiconosciuto(bool Indicato, TipoTrattamento? Trattamento)
{
    public static readonly TrattamentoOtaRiconosciuto NonIndicato = new(false, null);
}

/// <summary>
/// Ricava il trattamento venduto dall'OTA e compone le note della prenotazione.
///
/// L'unico dato strutturato è `boards`, e l'OTA lo compila solo per il proprio booking engine. Dai
/// portali (Booking, Expedia…) il trattamento arriva come testo nelle informazioni non standard, ed
/// è da quel testo che lo ricava anche il channel manager stesso. Per questo si legge in modo
/// prudente: prima `boards`, poi gli extra acquistati, poi il testo, e solo frasi esplicite; una frase
/// con una negazione o un "a pagamento" non vale ("Breakfast costs EUR 10" non è colazione inclusa).
/// Nel dubbio non si riconosce nulla: l'operatore legge comunque tutto nelle note.
/// Le richieste dell'ospite non contano: "vorremmo la colazione" non è un acquisto.
/// </summary>
public static class TrattamentoOta
{
    public const int LunghezzaMassimaNote = 4000;

    private static readonly (TipoTrattamento Tipo, string[] Frasi)[] FrasiIncluso =
    [
        (TipoTrattamento.AllInclusive, ["all inclusive", "all-inclusive", "allinclusive"]),
        (TipoTrattamento.PensioneCompleta, ["full board", "pensione completa", "vollpension", "pension complete", "pension completa"]),
        (TipoTrattamento.MezzaPensione, ["half board", "mezza pensione", "halbpension", "demi-pension", "demi pension", "media pension"]),
        (TipoTrattamento.Colazione, [
            "breakfast included", "breakfast is included", "breakfast: included", "includes breakfast", "including breakfast", "with breakfast",
            "colazione inclusa", "colazione compresa", "inclusa la colazione", "compresa la colazione", "con colazione",
            "fruhstuck inklusive", "inklusive fruhstuck", "mit fruhstuck", "petit-dejeuner inclus", "petit dejeuner inclus",
            "desayuno incluido",
        ]),
    ];

    private static readonly string[] FrasiSoloPernottamento = ["room only", "solo pernottamento", "no meals", "senza pasti", "nur ubernachtung"];

    // Una frase con una di queste non afferma un trattamento compreso nel prezzo.
    private static readonly string[] Negazioni =
    [
        "not ", "n't", "non ", "no breakfast", "excluded", "escluso", "esclusa", "nicht", "pas ",
        "cost", "extra", "surcharge", "supplement", "additional", "on request", "optional", "available", "possible",
        "a pagamento", "supplemento", "su richiesta", "disponibile", "aufpreis", "payant",
    ];

    private static readonly string[] ParoleColazione = ["breakfast", "colazione", "fruhstuck", "petit-dejeuner", "petit dejeuner", "desayuno"];

    // Chiavi delle informazioni non standard che possono portare dati di pagamento: non si salvano mai.
    private static readonly string[] ChiaviPagamento = ["card", "vcc", "cvv", "cvc", "expir", "carta", "scadenza", "payment", "iban"];

    private static readonly Regex NumeroCarta = new(@"\b(?:\d[ -]?){12,18}\d\b", RegexOptions.Compiled);

    public static TrattamentoOtaRiconosciuto Riconosci(DatiExtraOta? dati)
    {
        if (dati is null)
        {
            return TrattamentoOtaRiconosciuto.NonIndicato;
        }

        // Prima il dato strutturato. Con più camere e trattamenti diversi vale il più ampio: è quello
        // che la cucina deve comunque preparare.
        var daBoards = dati.Boards.Select(DaCodiceBoard).Where(t => t.Indicato).ToList();
        if (daBoards.Count > 0)
        {
            return PiuAmpio(daBoards);
        }

        // Il sito web della struttura manda il codice in ancillary (vedi ServiziSitoWeb): è un dato
        // strutturato come boards, vale prima del testo (dove "Colazione" da sola non basterebbe).
        if (ServiziSitoWeb.CodiceTrattamento(dati) is { } codiceSito && DaCodiceBoard(codiceSito) is { Indicato: true } daSito)
        {
            return daSito;
        }

        // Un extra comprato è un acquisto: niente controllo delle negazioni.
        var daExtra = dati.Extra
            .Select(e => Normalizza(e.Nome))
            .Select(nome => TipoDaNome(nome))
            .Where(t => t is not null)
            .Select(t => new TrattamentoOtaRiconosciuto(true, t))
            .ToList();
        if (daExtra.Count > 0)
        {
            return PiuAmpio(daExtra);
        }

        var daTesto = new List<TrattamentoOtaRiconosciuto>();
        // Solo il valore: le chiavi sono nomi tecnici del portale ("extra_info") e farebbero scattare
        // le negazioni su frasi che non ne hanno.
        foreach (var frase in dati.Ancillary.SelectMany(a => Frasi(a.Value)))
        {
            if (Negazioni.Any(frase.Contains))
            {
                continue;
            }

            if (FrasiSoloPernottamento.Any(frase.Contains))
            {
                daTesto.Add(new TrattamentoOtaRiconosciuto(true, null));
                continue;
            }

            foreach (var (tipo, frasi) in FrasiIncluso)
            {
                if (frasi.Any(frase.Contains))
                {
                    daTesto.Add(new TrattamentoOtaRiconosciuto(true, tipo));
                    break;
                }
            }
        }

        // Frasi in contraddizione (un trattamento e "solo pernottamento" insieme): meglio non scegliere.
        if (daTesto.Any(t => t.Trattamento is null) && daTesto.Any(t => t.Trattamento is not null))
        {
            return TrattamentoOtaRiconosciuto.NonIndicato;
        }

        return daTesto.Count > 0 ? PiuAmpio(daTesto) : TrattamentoOtaRiconosciuto.NonIndicato;
    }

    /// <summary>
    /// Testo leggibile per l'operatore con tutto quello che l'OTA ha mandato oltre ai dati standard.
    /// Senza chiavi di pagamento e con i numeri lunghi quanto una carta tolti (PCI-DSS: nemmeno per
    /// sbaglio). Null se non c'è niente da mostrare.
    /// </summary>
    public static string? ComponiNote(DatiExtraOta? dati)
    {
        if (dati is null)
        {
            return null;
        }

        var righe = new List<string>();
        if (!string.IsNullOrWhiteSpace(dati.NoteCliente))
        {
            righe.Add($"Richieste dell'ospite: {dati.NoteCliente.Trim()}");
        }

        foreach (var board in dati.Boards.Select(b => b.Trim().ToLowerInvariant()).Where(b => b != "").Distinct())
        {
            righe.Add($"Trattamento: {NomeBoard(board)}");
        }

        foreach (var extra in dati.Extra.Where(e => !string.IsNullOrWhiteSpace(e.Nome)))
        {
            var quantita = extra.Quantita > 1 ? $" x{extra.Quantita}" : "";
            var prezzo = extra.Prezzo > 0 ? $", {extra.Prezzo.ToString("N2", CultureInfo.GetCultureInfo("it-IT"))} €" : "";
            righe.Add($"Extra: {extra.Nome.Trim()}{quantita}{prezzo}");
        }

        foreach (var (chiave, valore) in dati.Ancillary.Distinct())
        {
            if (string.IsNullOrWhiteSpace(valore) || ChiaviPagamento.Any(c => chiave.Contains(c, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            righe.Add(string.IsNullOrWhiteSpace(chiave) ? valore.Trim() : $"{chiave}: {valore.Trim()}");
        }

        if (righe.Count == 0)
        {
            return null;
        }

        var testo = NumeroCarta.Replace(string.Join("\n", righe.Distinct()), "[numero rimosso]");
        return testo.Length <= LunghezzaMassimaNote ? testo : testo[..(LunghezzaMassimaNote - 1)] + "…";
    }

    private static TrattamentoOtaRiconosciuto DaCodiceBoard(string codice) => codice.Trim().ToLowerInvariant() switch
    {
        "bb" => new(true, TipoTrattamento.Colazione),
        "hb" => new(true, TipoTrattamento.MezzaPensione),
        "fb" => new(true, TipoTrattamento.PensioneCompleta),
        "ai" => new(true, TipoTrattamento.AllInclusive),
        "nb" => new(true, null),
        // Codici sconosciuti: restano nelle note.
        _ => TrattamentoOtaRiconosciuto.NonIndicato,
    };

    private static string NomeBoard(string codice) => codice switch
    {
        "bb" => "colazione",
        "hb" => "mezza pensione",
        "fb" => "pensione completa",
        "ai" => "all inclusive",
        "nb" => "solo pernottamento",
        _ => codice,
    };

    private static TipoTrattamento? TipoDaNome(string nome)
    {
        foreach (var (tipo, frasi) in FrasiIncluso)
        {
            if (frasi.Any(nome.Contains))
            {
                return tipo;
            }
        }

        return ParoleColazione.Any(nome.Contains) ? TipoTrattamento.Colazione : null;
    }

    private static TrattamentoOtaRiconosciuto PiuAmpio(List<TrattamentoOtaRiconosciuto> trovati) =>
        trovati.OrderByDescending(t => t.Trattamento is { } tipo ? (int)tipo : 0).First();

    private static IEnumerable<string> Frasi(string testo) =>
        Normalizza(testo).Split(['.', ';', '\n', '|', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Minuscolo, senza accenti e senza trattini bassi: "Frühstück" e "fruhstuck" devono combaciare.</summary>
    private static string Normalizza(string testo)
    {
        var scomposto = testo.ToLowerInvariant().Replace('_', ' ').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(scomposto.Length);
        foreach (var c in scomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
