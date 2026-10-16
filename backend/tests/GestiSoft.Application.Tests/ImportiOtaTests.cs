using GestiSoft.Application.Wubook;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Airbnb manda in `amount` il netto versato e la commissione in `ancillary`: il prezzo pagato
/// dall'ospite, quello da fatturare, è la somma. Numeri della prenotazione vera del 16/10/2026.
/// </summary>
public class ImportiOtaTests
{
    private static WubookPrenotazione Prenotazione(int idCanale, decimal importo, params (string Chiave, string Valore)[] ancillary) => new(
        RCode: 1788564304,
        ChannelReservationCode: "HMJUQRXJNT",
        CameraIdWubookRaw: "731368",
        CheckIn: new DateTime(2026, 9, 7),
        CheckOut: new DateTime(2026, 9, 12),
        Importo: importo,
        Adulti: 2,
        Bambini: 0,
        Status: 1,
        IdChannel: idCanale,
        CustomerName: null,
        CustomerSurname: null,
        CustomerEmail: null,
        CustomerCountry: null,
        CustomerCity: null,
        Extra: new DatiExtraOta(
            [],
            [new ExtraOta("PASS_THROUGH_CLEANING_FEE", 1, 100m)],
            ancillary.Select(a => new KeyValuePair<string, string>(a.Chiave, a.Valore)).ToList(),
            null));

    [Fact]
    public void Airbnb_CommissioneLetta_ELordoCoerente()
    {
        var booking = Prenotazione(43, 478.43m, ("Commission", "111.57000000000001"), ("Listing base price", "490.0"));

        Assert.Equal(111.57m, ImportiOta.Commissione(booking, "Airbnb"));
        Assert.Equal(590m, ImportiOta.LordoDaPrezzoBase(booking));
    }

    /// <summary>Riconosciuto anche dal nome, se un giorno cambiasse l'id del tipo di canale.</summary>
    [Fact]
    public void Airbnb_DalNomeDelCanale()
    {
        Assert.Equal(111.57m, ImportiOta.Commissione(Prenotazione(999, 478.43m, ("Commission", "111.57 EUR")), "Airbnb"));
    }

    /// <summary>Per gli altri portali non è verificato che amount sia il netto: sommare raddoppierebbe la commissione.</summary>
    [Fact]
    public void AltroPortale_ConCommissione_NonSiTocca()
    {
        Assert.Null(ImportiOta.Commissione(Prenotazione(2, 500m, ("Commission", "75.00")), "Booking.com"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("non disponibile")]
    public void Airbnb_SenzaCommissioneValida_Null(string valore)
    {
        Assert.Null(ImportiOta.Commissione(Prenotazione(43, 478.43m, ("Commission", valore)), "Airbnb"));
    }
}
