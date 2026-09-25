using GestiSoft.Application.Camere;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Prezzo derivato per occupazione, come su Booking: il prezzo della tipologia vale per gli ospiti
/// inclusi, e ogni ospite in meno toglie una riduzione facoltativa, in euro o in percentuale.
/// </summary>
public class RiduzioneOspitiInMenoTests
{
    private static SettingTipologia Tipologia(decimal? riduzione, TipoVariazionePrezzo tipo = TipoVariazionePrezzo.Euro, int inclusi = 2) =>
        new() { NumeroImplementoPersona = inclusi, RiduzioneOspiteInMeno = riduzione, TipoRiduzioneOspiteInMeno = tipo };

    [Fact]
    public void SenzaRiduzione_IlPrezzoNonCambia()
    {
        Assert.Equal(95m, PrezziCameraService.PrezzoNotteConRiduzione(95m, Tipologia(null), 1));
    }

    [Fact]
    public void UsoSingolaInEuro()
    {
        Assert.Equal(86m, PrezziCameraService.PrezzoNotteConRiduzione(95m, Tipologia(9m), 1));
    }

    [Fact]
    public void UsoSingolaInPercentuale()
    {
        Assert.Equal(85.5m, PrezziCameraService.PrezzoNotteConRiduzione(95m, Tipologia(10m, TipoVariazionePrezzo.Percentuale), 1));
    }

    [Fact]
    public void OgniOspiteInMenoTogliePiuRiduzioni()
    {
        // Quadrupla a 100 €, occupata da una sola persona: tre ospiti in meno.
        Assert.Equal(70m, PrezziCameraService.PrezzoNotteConRiduzione(100m, Tipologia(10m, inclusi: 4), 1));
    }

    [Fact]
    public void ConGliOspitiInclusiOPiu_NessunaRiduzione()
    {
        Assert.Equal(95m, PrezziCameraService.PrezzoNotteConRiduzione(95m, Tipologia(9m), 2));
        Assert.Equal(95m, PrezziCameraService.PrezzoNotteConRiduzione(95m, Tipologia(9m), 3));
    }

    [Fact]
    public void MaiSottoZero()
    {
        Assert.Equal(0m, PrezziCameraService.PrezzoNotteConRiduzione(20m, Tipologia(30m), 1));
    }
}
