using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Tests;

/// <summary>Il ricevuto non si salva: è il totale meno la commissione, uguale al totale senza commissione.</summary>
public class RicevutoPrenotazioneTests
{
    [Theory]
    [InlineData(590, 111.57, 478.43)]
    [InlineData(300, null, 300)]
    [InlineData(300, 0.0, 300)]
    public void TotaleMenoCommissione(double totale, double? commissione, double atteso)
    {
        Assert.Equal((decimal)atteso, Prenotazione.Ricevuto((decimal)totale, (decimal?)commissione));
    }

    [Fact]
    public void SenzaTotale_NessunRicevuto()
    {
        Assert.Null(Prenotazione.Ricevuto(null, 10m));
    }
}
