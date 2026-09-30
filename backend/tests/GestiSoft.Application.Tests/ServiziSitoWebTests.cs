using GestiSoft.Application.Wubook;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Servizi extra venduti dal sito web, letti da `ancillary` "servizi" delle prenotazioni WuBook:
/// "CODICE:QUANTITÀ:PREZZO_UNITARIO" separati da ";". Quello che non rispetta il formato si scarta.
/// </summary>
public class ServiziSitoWebTests
{
    private static DatiExtraOta Dati(params (string Chiave, string Valore)[] ancillary) =>
        new([], [], ancillary.Select(a => new KeyValuePair<string, string>(a.Chiave, a.Valore)).ToList(), null);

    [Fact]
    public void FormatoDelSito_SiLeggeRigaPerRiga()
    {
        var righe = ServiziSitoWeb.Leggi(Dati(("servizi", "SPA:2:50.00;MASSAGGIO_60:1:80.00")));

        Assert.Equal([new ServizioSitoWeb("SPA", 2, 50.00m), new ServizioSitoWeb("MASSAGGIO_60", 1, 80.00m)], righe);
    }

    [Fact]
    public void PrezzoConIlPunto_NonDipendeDallaLinguaDelServer()
    {
        var riga = Assert.Single(ServiziSitoWeb.Leggi(Dati(("servizi", "SPA:1:12.50"))));

        Assert.Equal(12.50m, riga.PrezzoUnitario);
    }

    [Fact]
    public void SenzaChiaveServizi_NessunaRiga()
    {
        Assert.Empty(ServiziSitoWeb.Leggi(Dati(("adulti", "2"), ("trattamento", "Mezza pensione"))));
        Assert.Empty(ServiziSitoWeb.Leggi(null));
    }

    [Theory]
    [InlineData("spa:1:50.00")]        // codice minuscolo: non è il formato del sito
    [InlineData("SPA:0:50.00")]        // quantità zero
    [InlineData("SPA:due:50.00")]      // quantità non numerica
    [InlineData("SPA:1:50,00")]        // prezzo con la virgola
    [InlineData("SPA:1")]              // manca il prezzo
    [InlineData("Colazione inclusa")]  // testo libero di un portale
    public void PartiFuoriFormato_SiScartano(string valore)
    {
        Assert.Empty(ServiziSitoWeb.Leggi(Dati(("servizi", valore))));
    }

    [Fact]
    public void UnaParteSbagliata_NonFaPerdereLeAltre()
    {
        var riga = Assert.Single(ServiziSitoWeb.Leggi(Dati(("servizi", "???;SPA:2:50.00;"))));

        Assert.Equal(new ServizioSitoWeb("SPA", 2, 50.00m), riga);
    }

    [Fact]
    public void StessoCodiceEPrezzo_SiSommaLaQuantita()
    {
        var riga = Assert.Single(ServiziSitoWeb.Leggi(Dati(("servizi", "SPA:1:50.00;SPA:2:50.00"))));

        Assert.Equal(3, riga.Quantita);
    }
}
