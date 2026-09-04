using GestiSoft.Application.Statistiche;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Copre le formule della pagina Statistiche portate dal gestionale legacy (DashBoardViewModel.cs,
/// WPF): il tasso di occupazione (numero notti / (camere × giorni trascorsi anno)) e l'aggregazione
/// Top7+Altro usata per i grafici "prenotazioni per agenzia/nazionalità" e "ricavo per tipologia".
/// </summary>
public class StatisticheServiceTests
{
    [Fact]
    public void GiorniTrascorsiAnno_AnnoCorrente_UsaIlGiornoDellAnnoOdierno()
    {
        var annoCorrente = DateTime.UtcNow.Year;

        Assert.Equal(DateTime.UtcNow.DayOfYear, CalcoloOccupazione.GiorniTrascorsiAnno(annoCorrente));
    }

    [Fact]
    public void GiorniTrascorsiAnno_AnnoPassatoBisestile_Restituisce366()
    {
        Assert.Equal(366, CalcoloOccupazione.GiorniTrascorsiAnno(2024));
    }

    [Fact]
    public void GiorniTrascorsiAnno_AnnoPassatoNonBisestile_Restituisce365()
    {
        Assert.Equal(365, CalcoloOccupazione.GiorniTrascorsiAnno(2023));
    }

    [Fact]
    public void GiorniTrascorsiAnno_AnnoFuturo_RestituisceZero()
    {
        Assert.Equal(0, CalcoloOccupazione.GiorniTrascorsiAnno(DateTime.UtcNow.Year + 1));
    }

    [Fact]
    public void TassoOccupazionePercentuale_NessunaCamera_RestituisceZero()
    {
        Assert.Equal(0, CalcoloOccupazione.TassoOccupazionePercentuale(sommaPermanenzaNotti: 100, numeroCamere: 0, anno: 2023));
    }

    [Fact]
    public void TassoOccupazionePercentuale_AnnoFuturo_RestituisceZero()
    {
        Assert.Equal(0, CalcoloOccupazione.TassoOccupazionePercentuale(sommaPermanenzaNotti: 100, numeroCamere: 4, anno: DateTime.UtcNow.Year + 1));
    }

    [Fact]
    public void TassoOccupazionePercentuale_SuperaIlPienoCarico_VieneCappatoAl100()
    {
        // 4 camere × 365 giorni = 1460 notti disponibili nell'anno passato; 2000 notti soggiornate
        // (es. dati storici sporchi/camere aggiunte dopo) non devono restituire oltre il 100%.
        var tasso = CalcoloOccupazione.TassoOccupazionePercentuale(sommaPermanenzaNotti: 2000, numeroCamere: 4, anno: 2023);

        Assert.Equal(100, tasso);
    }

    [Fact]
    public void TassoOccupazionePercentuale_CasoNormale_CalcolaLaPercentualeArrotondata()
    {
        // 2 camere × 365 giorni = 730 notti disponibili; 365 notti soggiornate = 50.0%.
        var tasso = CalcoloOccupazione.TassoOccupazionePercentuale(sommaPermanenzaNotti: 365, numeroCamere: 2, anno: 2023);

        Assert.Equal(50.0, tasso);
    }

    [Fact]
    public void Top7PiuAltroConteggio_FinoASetteVoci_NonAggiungeAltro()
    {
        var voci = new[] { ("Booking.com", 10), ("Diretta", 8), ("Airbnb", 3) };

        var risultato = StatisticheService.Top7PiuAltroConteggio(voci);

        Assert.Equal(3, risultato.Count);
        Assert.DoesNotContain(risultato, v => v.Etichetta == "Altro");
    }

    [Fact]
    public void Top7PiuAltroConteggio_PiuDiSetteVoci_AggregaIlRestoInAltro()
    {
        var voci = Enumerable.Range(1, 9).Select(i => ($"Agenzia{i}", 10 - i)).ToArray();

        var risultato = StatisticheService.Top7PiuAltroConteggio(voci);

        Assert.Equal(8, risultato.Count); // 7 top + "Altro"
        Assert.Equal("Altro", risultato[^1].Etichetta);
        // Le due voci più piccole (Agenzia8=2, Agenzia9=1) finiscono in "Altro".
        Assert.Equal(3, risultato[^1].Conteggio);
        Assert.Equal("Agenzia1", risultato[0].Etichetta); // la più grande (9) resta prima, ordinamento decrescente.
    }

    [Fact]
    public void Top7PiuAltroImporto_PiuDiSetteVoci_AggregaIlRestoInAltro()
    {
        var voci = Enumerable.Range(1, 8).Select(i => ($"Tipologia{i}", (decimal)i * 100)).ToArray();

        var risultato = StatisticheService.Top7PiuAltroImporto(voci);

        Assert.Equal(8, risultato.Count);
        Assert.Equal("Altro", risultato[^1].Etichetta);
        Assert.Equal(100m, risultato[^1].Importo); // solo Tipologia1 (100) resta fuori dalla Top7.
    }

    [Fact]
    public void ConPercentuali_CalcolaSulTotaleComplessivoNonSoloSuiPresentiInTop7()
    {
        var voci = new (string? Etichetta, int Conteggio)[] { ("Italia", 60), ("Francia", 40) };

        var risultato = StatisticheService.ConPercentuali(voci, totale: 100);

        Assert.Equal(60.0, risultato.Single(v => v.Etichetta == "Italia").Percentuale);
        Assert.Equal(40.0, risultato.Single(v => v.Etichetta == "Francia").Percentuale);
    }

    [Fact]
    public void ConPercentuali_EtichettaNulla_DiventaSconosciuto()
    {
        var voci = new (string? Etichetta, int Conteggio)[] { (null, 5) };

        var risultato = StatisticheService.ConPercentuali(voci, totale: 5);

        Assert.Equal("Sconosciuto", risultato.Single().Etichetta);
    }
}
