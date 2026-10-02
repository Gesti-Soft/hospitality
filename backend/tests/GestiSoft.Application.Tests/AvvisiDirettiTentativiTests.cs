using GestiSoft.Application.Wubook;

namespace GestiSoft.Application.Tests;

/// <summary>Tentativi di importazione di un avviso diretto dell'OTA: attese crescenti per circa 24 ore.</summary>
public class AvvisiDirettiTentativiTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(6, 32)]
    [InlineData(7, 60)]
    [InlineData(29, 60)]
    public void Attese_CresconoFinoAUnOra(int tentativiFalliti, int minutiAttesi)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutiAttesi), WubookAvvisiDirettiService.AttesaDopo(tentativiFalliti));
    }

    [Fact]
    public void TuttiITentativi_CopronoAlmenoUnGiorno()
    {
        // Le attese tra un tentativo e il successivo, dal primo fallito all'ultimo.
        var totale = Enumerable.Range(1, WubookAvvisiDirettiService.TentativiMassimi - 1)
            .Aggregate(TimeSpan.Zero, (somma, n) => somma + WubookAvvisiDirettiService.AttesaDopo(n));

        Assert.True(totale >= TimeSpan.FromHours(24), $"Coperte solo {totale.TotalHours:F1} ore.");
    }
}
