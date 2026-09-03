using GestiSoft.Application.Ospiti;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Copre il bug reale segnalato dall'utente: un ospite residente a "Castellammare del Golfo" non
/// veniva riconosciuto come residente (niente riduzione tassa di soggiorno) perché il confronto era
/// per stringa esatta, mentre i campi Comune sono salvati come "COMUNE (PROVINCIA)" — bastava che uno
/// dei due campi avesse il suffisso provincia e l'altro no per far fallire il confronto.
/// </summary>
public class OspitiServiceTests
{
    [Theory]
    [InlineData("Castellammare del Golfo (TP)", "Castellammare del Golfo")]
    [InlineData("Castellammare del Golfo", "Castellammare del Golfo (TP)")]
    [InlineData("Castellammare del Golfo (TP)", "Castellammare del Golfo (TP)")]
    [InlineData("castellammare del golfo (tp)", "CASTELLAMMARE DEL GOLFO (TP)")]
    public void EsenteResidenza_ComuneConOSenzaProvincia_Riconosce(string luogoResidenza, string comuneStruttura)
    {
        Assert.True(OspitiService.EsenteResidenza(luogoResidenza, comuneStruttura));
    }

    [Fact]
    public void EsenteResidenza_ComuneDiverso_NonRiconosce()
    {
        Assert.False(OspitiService.EsenteResidenza("Palermo (PA)", "Castellammare del Golfo (TP)"));
    }

    [Theory]
    [InlineData(null, "Castellammare del Golfo (TP)")]
    [InlineData("Castellammare del Golfo (TP)", null)]
    [InlineData("", "Castellammare del Golfo (TP)")]
    public void EsenteResidenza_CampoMancante_NonRiconosce(string? luogoResidenza, string? comuneStruttura)
    {
        Assert.False(OspitiService.EsenteResidenza(luogoResidenza, comuneStruttura));
    }
}
