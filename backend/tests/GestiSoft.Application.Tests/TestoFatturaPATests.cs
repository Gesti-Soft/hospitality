using GestiSoft.Application.Fatturazione;

namespace GestiSoft.Application.Tests;

/// <summary>Il tracciato FatturaPA ammette solo Latin-1: un carattere fuori fa scartare il file.</summary>
public class TestoFatturaPATests
{
    [Theory]
    [InlineData("Soggiorno — camera 12", "Soggiorno - camera 12")]
    [InlineData("L’ospite “VIP”", "L'ospite \"VIP\"")]
    [InlineData("Totale 10 €", "Totale 10 EUR")]
    [InlineData("Riga uno\r\nriga due", "Riga uno riga due")]
    [InlineData("Attendere…", "Attendere...")]
    [InlineData("Città è più", "Città è più")]
    [InlineData("Dvořák", "Dvorák")]
    [InlineData("Łódź", "Lódz")]
    [InlineData("Ristorante 🍝 da Mario", "Ristorante da Mario")]
    public void Traslittera_SoloLatin1(string testo, string atteso)
    {
        Assert.Equal(atteso, TestoFatturaPA.Pulisci(testo, 1000));
    }

    [Fact]
    public void Taglia_DopoLaTraslitterazione()
    {
        Assert.Equal("ab...", TestoFatturaPA.Pulisci("ab…cd", 5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("🍝")]
    public void Vuoto_DiventaNull(string? testo)
    {
        Assert.Null(TestoFatturaPA.Pulisci(testo, 60));
    }
}
