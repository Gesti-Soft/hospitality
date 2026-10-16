using System.Globalization;

namespace GestiSoft.Application.Wubook;

/// <summary>
/// Prezzo pagato dall'ospite, commissione del portale e importo versato alla struttura. La fattura
/// all'ospite va sempre sul prezzo pagato, anche quando il portale versa meno.
///
/// Per Airbnb `amount` è il netto versato, non il prezzo pagato dall'ospite: la commissione arriva a
/// parte, in `ancillary` alla chiave "Commission". Verificato il 16/10/2026 su una prenotazione vera
/// (amount 478,43 + Commission 111,57 = 590,00, come nell'estratto di Airbnb; "Listing base price"
/// 490 + pulizia 100 dà lo stesso lordo). Per gli altri portali non è verificato se `amount` sia
/// lordo o netto: senza una prenotazione vera, sommarci una commissione rischierebbe di gonfiare la
/// fattura, quindi qui restano come prima.
/// </summary>
public static class ImportiOta
{
    /// <summary>Tipo di canale di Airbnb su WuBook (id_channel).</summary>
    public const int IdCanaleAirbnb = 43;

    public const string ChiaveCommissione = "Commission";
    public const string ChiavePrezzoBase = "Listing base price";

    /// <summary>Commissione del portale da sommare ad `amount` per avere il prezzo pagato dall'ospite; null se non si applica.</summary>
    public static decimal? Commissione(WubookPrenotazione booking, string nomeCanale)
    {
        if (!Airbnb(booking, nomeCanale) || Numero(Valore(booking.Extra, ChiaveCommissione)) is not { } commissione || commissione <= 0)
        {
            return null;
        }

        return Math.Round(commissione, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Prezzo pagato dall'ospite ricostruito da "Listing base price" più gli extra (la pulizia), solo
    /// per controllare la somma con la commissione; null se il prezzo base non c'è.
    /// </summary>
    public static decimal? LordoDaPrezzoBase(WubookPrenotazione booking) =>
        Numero(Valore(booking.Extra, ChiavePrezzoBase)) is { } prezzoBase
            ? prezzoBase + (booking.Extra?.Extra.Sum(e => e.Prezzo) ?? 0)
            : null;

    private static bool Airbnb(WubookPrenotazione booking, string nomeCanale) =>
        booking.IdChannel == IdCanaleAirbnb || nomeCanale.Contains("airbnb", StringComparison.OrdinalIgnoreCase);

    private static string? Valore(DatiExtraOta? dati, string chiave) =>
        dati?.Ancillary.FirstOrDefault(a => string.Equals(a.Key, chiave, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>Numero col punto, a volte seguito dalla valuta ("111.57 EUR"), a volte con decimali spuri ("111.57000000000001").</summary>
    private static decimal? Numero(string? testo)
    {
        var pulito = testo?.Replace("EUR", "", StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(pulito, NumberStyles.Number, CultureInfo.InvariantCulture, out var valore) ? valore : null;
    }
}
