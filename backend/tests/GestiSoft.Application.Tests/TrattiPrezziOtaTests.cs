using GestiSoft.Application.Wubook;

namespace GestiSoft.Application.Tests;

/// <summary>
/// I prezzi verso l'OTA partono a tratti consecutivi: WuBook rifiuta i prezzi a zero, quindi i
/// giorni senza prezzo non si mandano e restano sul portale com'erano.
/// </summary>
public class TrattiPrezziOtaTests
{
    private static readonly DateTime Primo = new(2026, 11, 1);

    private static List<(DateTime, decimal?)> Giorni(params decimal?[] prezzi) =>
        prezzi.Select((p, i) => (Primo.AddDays(i), p)).ToList();

    [Fact]
    public void TuttiConPrezzo_UnSoloTratto()
    {
        var tratti = WubookPrezziService.Tratti(Giorni(100m, 100m, 120m)).ToList();

        var tratto = Assert.Single(tratti);
        Assert.Equal(Primo, tratto.Inizio);
        Assert.Equal([100m, 100m, 120m], tratto.Prezzi);
    }

    [Fact]
    public void BucoInMezzo_DueTrattiDaInviareEUnoDaSegnalare()
    {
        var tratti = WubookPrezziService.Tratti(Giorni(100m, null, null, 90m)).ToList();

        Assert.Equal(3, tratti.Count);
        Assert.Equal([100m], tratti[0].Prezzi);
        Assert.Null(tratti[1].Prezzi);
        Assert.Equal(Primo.AddDays(1), tratti[1].Inizio);
        Assert.Equal(2, tratti[1].Giorni);
        Assert.Equal(Primo.AddDays(3), tratti[2].Inizio);
        Assert.Equal([90m], tratti[2].Prezzi);
    }

    [Fact]
    public void NessunPrezzo_NienteDaInviare()
    {
        var tratto = Assert.Single(WubookPrezziService.Tratti(Giorni(null, null)));
        Assert.Null(tratto.Prezzi);
        Assert.Equal(2, tratto.Giorni);
    }
}
