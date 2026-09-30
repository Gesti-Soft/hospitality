using System.Globalization;
using System.Text.RegularExpressions;

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

    /// <summary>Autore delle righe aggiunte dall'import: le distingue da quelle aggiunte a mano, che l'import non tocca.</summary>
    public const string AggiuntoDa = "sito web";

    /// <summary>Stesso formato del codice in Impostazioni → Servizi: maiuscole, cifre, "_" e "-".</summary>
    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]{1,29}$")]
    public static partial Regex FormatoCodice();

    /// <summary>
    /// Le righe riconoscibili della chiave "servizi"; una parte che non rispetta il formato si scarta
    /// (un portale potrebbe usare la stessa chiave per altro). Lo stesso codice due volte si somma.
    /// </summary>
    public static IReadOnlyList<ServizioSitoWeb> Leggi(DatiExtraOta? dati)
    {
        var valore = dati?.Ancillary.FirstOrDefault(a => string.Equals(a.Key, ChiaveAncillary, StringComparison.OrdinalIgnoreCase)).Value;
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
