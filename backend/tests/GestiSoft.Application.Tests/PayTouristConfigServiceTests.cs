using GestiSoft.Application.PayTourist;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Copre il bug reale trovato con i dati reali di PayTourist per Castellammare del Golfo: il nome
/// "Anziani Ultrasettantacinquenni  - 100%" non scrive l'età in cifre (solo la percentuale finale),
/// l'età va letta dalla descrizione ("...oltre il compimento del 75° anno di età."). Una prima
/// versione della regex di esclusione della percentuale, per backtracking del quantificatore
/// greedy, leggeva "10" da "100%" invece di scartare il numero e cadere sulla descrizione.
/// </summary>
public class PayTouristConfigServiceTests
{
    private static readonly PayTouristRiduzioneDto[] RiduzioniCastellammareDelGolfo =
    [
        new(5, "Anziani Ultrasettantacinquenni  - 100%", "Sono esenti dal pagamento dell'imposta gli anziani oltre il compimento del 75° anno di età.", "100.00"),
        new(2, "Esenzione  - 100%", "Sono esenti dal pagamento dell'imposta di soggiorno: i disabili...", "100.00"),
        new(1, "Esenzione Minore Anni 12 - 100%", "Sono esenti dal pagamento dell'imposta di soggiorno i minori entro il dodicesimo anno di età;", "100.00"),
        new(3, "Prolungamento Soggiorno - 100%", "Sono esenti dal pagamento i soggetti che pernottano...", "100.00"),
        new(4, "Residenti nel Comune - 100%", "Sono esenti dal pagamento dell'imposta i residenti nel comune di Castellammare del Golfo;", "100.00"),
    ];

    [Fact]
    public void EstraiEta_Minori_LeggeLaCifraDalNome()
    {
        var candidata = PayTouristConfigService.TrovaRiduzione(RiduzioniCastellammareDelGolfo, "minor");

        Assert.Equal(12, PayTouristConfigService.EstraiEta(candidata));
    }

    [Fact]
    public void EstraiEta_Anziani_IgnoraLaPercentualeNelNomeELeggeDallaDescrizione()
    {
        var candidata = PayTouristConfigService.TrovaRiduzione(RiduzioniCastellammareDelGolfo, "anzian", "ultra");

        Assert.Equal(75, PayTouristConfigService.EstraiEta(candidata));
    }

    [Fact]
    public void TrovaRiduzione_NessunaCorrispondenza_RestituisceNull()
    {
        var candidata = PayTouristConfigService.TrovaRiduzione(RiduzioniCastellammareDelGolfo, "disabil");

        Assert.Null(candidata);
        Assert.Null(PayTouristConfigService.EstraiEta(candidata));
        Assert.Null(PayTouristConfigService.EstraiPercentuale(candidata));
    }

    [Fact]
    public void EstraiPercentuale_LeggeIlCampoStrutturato()
    {
        var residenti = PayTouristConfigService.TrovaRiduzione(RiduzioniCastellammareDelGolfo, "resid");

        Assert.Equal(100m, PayTouristConfigService.EstraiPercentuale(residenti));
    }
}
