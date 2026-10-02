using System.Globalization;
using System.Text.RegularExpressions;
using GestiSoft.Application.Trattamenti;

namespace GestiSoft.Application.Wubook;

/// <summary>Servizio extra venduto dal sito web insieme alla prenotazione: codice, quantità e prezzo unitario pagato.</summary>
public record ServizioSitoWeb(string Codice, int Quantita, decimal PrezzoUnitario);

/// <summary>
/// Servizi extra (SPA, massaggi…) venduti dal sito web della struttura, che li manda a WuBook in
/// `ancillary`, chiave "servizi", con un formato fisso: "CODICE:QUANTITÀ:PREZZO_UNITARIO" separati da
/// ";", prezzo con il punto (es. "SPA:2:50.00;MASSAGGIO_60:1:80.00"). Il codice è lo stesso del
/// servizio in Impostazioni → Servizi (<see cref="Domain.Entities.ServizioStruttura.Codice"/>).
/// Arriva solo dalle prenotazioni fatte sul sito: i portali non mandano questa chiave.
/// </summary>
public static partial class ServiziSitoWeb
{
    public const string ChiaveAncillary = "servizi";

    /// <summary>Codice WuBook del trattamento venduto (bb, hb, fb, ai; nb = solo pernottamento): vedi TrattamentoOta.</summary>
    public const string ChiaveCodiceTrattamento = "trattamento_codice";

    private const string ChiavePrezzoAdulto = "trattamento_prezzo_adulto";
    private const string ChiavePrezzoBambino = "trattamento_prezzo_bambino";
    private const string ChiaveEtaBambini = "trattamento_eta_bambini";

    /// <summary>Autore delle righe aggiunte dall'import: le distingue da quelle aggiunte a mano, che l'import non tocca.</summary>
    public const string AggiuntoDa = "sito web";

    /// <summary>Stesso formato del codice in Impostazioni → Servizi: maiuscole, cifre, "_" e "-".</summary>
    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]{1,29}$")]
    public static partial Regex FormatoCodice();

    private const string ChiaveOraArrivo = "ora_arrivo";
    private const string ChiaveAnimale = "animale";

    /// <summary>
    /// Intestazione del blocco di dettagli (ospiti, trattamento, animale…) che il sito accoda alle
    /// richieste dell'ospite per chi legge su WuBook: qui quei dati arrivano già in `ancillary`.
    /// </summary>
    private const string IntestazioneDettagliSito = "Dettagli prenotazione (sito web)";

    public static string? CodiceTrattamento(DatiExtraOta? dati) => Valore(dati, ChiaveCodiceTrattamento);

    /// <summary>Ora di arrivo indicata sul sito ("15:00"), null se assente.</summary>
    public static string? OraArrivo(DatiExtraOta? dati) =>
        Valore(dati, ChiaveOraArrivo) is { } ora && !string.IsNullOrWhiteSpace(ora) ? ora.Trim() : null;

    /// <summary>Se l'ospite ha dichiarato un animale sul sito; null se il dato non c'è (prenotazioni dai portali).</summary>
    public static bool? Animale(DatiExtraOta? dati) => Valore(dati, ChiaveAnimale)?.Trim().ToLowerInvariant() switch
    {
        "si" or "sì" => true,
        "no" => false,
        _ => null,
    };

    /// <summary>Le richieste dell'ospite senza il blocco di dettagli accodato dal sito (vedi sopra).</summary>
    public static string? SenzaDettagliSito(string? note)
    {
        if (note is null)
        {
            return null;
        }

        var indice = note.IndexOf(IntestazioneDettagliSito, StringComparison.OrdinalIgnoreCase);
        if (indice >= 0)
        {
            // Il blocco parte dall'inizio della riga dell'intestazione ("--- Dettagli … ---").
            var inizioRiga = note.LastIndexOf('\n', indice) + 1;
            note = note[..inizioRiga];
        }

        return note.Trim();
    }

    /// <summary>
    /// Prezzi a persona e a notte a cui il sito ha venduto il trattamento, null se non li manda (o
    /// non sono leggibili). Il prezzo bambini vale solo insieme all'età massima, come nel listino.
    /// </summary>
    public static PrezziTrattamento? LeggiPrezziTrattamento(DatiExtraOta? dati)
    {
        if (!Decimale(Valore(dati, ChiavePrezzoAdulto), out var adulto))
        {
            return null;
        }

        var bambino = Decimale(Valore(dati, ChiavePrezzoBambino), out var prezzoBambino) ? prezzoBambino : (decimal?)null;
        var eta = int.TryParse(Valore(dati, ChiaveEtaBambini), NumberStyles.None, CultureInfo.InvariantCulture, out var anni) && anni <= 17
            ? anni
            : (int?)null;
        return bambino is not null && eta is not null
            ? new PrezziTrattamento(adulto, bambino, eta)
            : new PrezziTrattamento(adulto, null, null);
    }

    private static string? Valore(DatiExtraOta? dati, string chiave) =>
        dati?.Ancillary.FirstOrDefault(a => string.Equals(a.Key, chiave, StringComparison.OrdinalIgnoreCase)).Value;

    private static bool Decimale(string? testo, out decimal valore)
    {
        valore = 0;
        return testo is not null
            && decimal.TryParse(testo.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valore)
            && valore >= 0;
    }

    /// <summary>
    /// Le righe riconoscibili della chiave "servizi"; una parte che non rispetta il formato si scarta
    /// (un portale potrebbe usare la stessa chiave per altro). Lo stesso codice due volte si somma.
    /// </summary>
    public static IReadOnlyList<ServizioSitoWeb> Leggi(DatiExtraOta? dati)
    {
        var valore = Valore(dati, ChiaveAncillary);
        if (string.IsNullOrWhiteSpace(valore))
        {
            return [];
        }

        var righe = new List<ServizioSitoWeb>();
        foreach (var parte in valore.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var campi = parte.Split(':', StringSplitOptions.TrimEntries);
            if (campi.Length != 3
                || !FormatoCodice().IsMatch(campi[0])
                || !int.TryParse(campi[1], NumberStyles.None, CultureInfo.InvariantCulture, out var quantita)
                || quantita < 1
                || !decimal.TryParse(campi[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var prezzo))
            {
                continue;
            }

            var stessa = righe.FindIndex(r => r.Codice == campi[0] && r.PrezzoUnitario == prezzo);
            if (stessa >= 0)
            {
                righe[stessa] = righe[stessa] with { Quantita = righe[stessa].Quantita + quantita };
            }
            else
            {
                righe.Add(new ServizioSitoWeb(campi[0], quantita, prezzo));
            }
        }

        return righe;
    }
}
