using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Contracts.Camere;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Supplemento per persona in più con fasce d'età, sul modello di Booking: importo per fascia,
/// supplemento pieno per adulti ed età fuori fascia. Esempio della struttura: due ospiti inclusi,
/// 20 € a notte per persona in più, gratis fino a 13 anni, 10 € da 14 a 17.
/// </summary>
public class SupplementoFasceEtaTests
{
    private static readonly SettingTipologia Tipologia = new() { NumeroImplementoPersona = 2, Implemento = 20m };

    private static readonly FasciaEtaSupplemento[] Fasce =
    [
        new() { EtaMin = 0, EtaMax = 13, ImportoPerNotte = 0m },
        new() { EtaMin = 14, EtaMax = 17, ImportoPerNotte = 10m },
    ];

    [Fact]
    public void SenzaFasce_OgniOspiteInPiuPagaIlSupplementoPieno()
    {
        Assert.Equal(20m, PrezziCameraService.SupplementoPerNotte(Tipologia, [], 3, [15], 100m));
    }

    [Fact]
    public void EntroGliOspitiInclusi_NessunSupplemento()
    {
        Assert.Equal(0m, PrezziCameraService.SupplementoPerNotte(Tipologia, Fasce, 2, [15], 100m));
    }

    [Fact]
    public void DueAdultiEUnRagazzoDi15Anni_PagaLaFasciaRidotta()
    {
        Assert.Equal(10m, PrezziCameraService.SupplementoPerNotte(Tipologia, Fasce, 3, [15], 100m));
    }

    [Fact]
    public void DueAdultiEUnBambinoDi8Anni_Gratis()
    {
        Assert.Equal(0m, PrezziCameraService.SupplementoPerNotte(Tipologia, Fasce, 3, [8], 100m));
    }

    [Fact]
    public void TreAdulti_IlTerzoPagaPieno()
    {
        Assert.Equal(20m, PrezziCameraService.SupplementoPerNotte(Tipologia, Fasce, 3, [], 100m));
    }

    /// <summary>I posti inclusi vanno ai più grandi: con un adulto e due ragazzi paga il più piccolo, non l'adulto.</summary>
    [Fact]
    public void UnAdultoEDueFigli_IPostiInclusiVannoAiPiuGrandi()
    {
        // Adulto e ragazzo di 16 anni nei posti inclusi, paga il bambino di 6: gratis.
        Assert.Equal(0m, PrezziCameraService.SupplementoPerNotte(Tipologia, Fasce, 3, [6, 16], 100m));
    }

    [Fact]
    public void DueAdultiEDueFigli_OgnunoPagaLaSuaFascia()
    {
        Assert.Equal(10m, PrezziCameraService.SupplementoPerNotte(Tipologia, Fasce, 4, [6, 16], 100m));
    }

    /// <summary>Come Booking: un'età che non rientra in nessuna fascia paga come un adulto.</summary>
    [Fact]
    public void EtaFuoriDaOgniFascia_PagaIlSupplementoPieno()
    {
        FasciaEtaSupplemento[] soloPiccoli = [new() { EtaMin = 0, EtaMax = 2, ImportoPerNotte = 0m }];
        Assert.Equal(20m, PrezziCameraService.SupplementoPerNotte(Tipologia, soloPiccoli, 3, [10], 100m));
    }

    /// <summary>Supplemento in percentuale: si calcola sul prezzo della camera di quella notte.</summary>
    [Fact]
    public void SupplementoInPercentuale_SulPrezzoDellaNotte()
    {
        var inPercentuale = new SettingTipologia { NumeroImplementoPersona = 2, Implemento = 20m, TipoImplemento = TipoVariazionePrezzo.Percentuale };
        Assert.Equal(19m, PrezziCameraService.SupplementoPerNotte(inPercentuale, [], 3, [], 95m));
    }

    /// <summary>Fascia in percentuale: si calcola sul supplemento pieno, "da 14 a 17 anni il 50%".</summary>
    [Fact]
    public void FasciaInPercentuale_SulSupplementoPieno()
    {
        FasciaEtaSupplemento[] meta = [new() { EtaMin = 14, EtaMax = 17, ImportoPerNotte = 50m, TipoImporto = TipoVariazionePrezzo.Percentuale }];
        Assert.Equal(10m, PrezziCameraService.SupplementoPerNotte(Tipologia, meta, 3, [15], 100m));
    }

    [Fact]
    public void SupplementoEFasciaEntrambiInPercentuale()
    {
        var inPercentuale = new SettingTipologia { NumeroImplementoPersona = 2, Implemento = 20m, TipoImplemento = TipoVariazionePrezzo.Percentuale };
        FasciaEtaSupplemento[] meta = [new() { EtaMin = 14, EtaMax = 17, ImportoPerNotte = 50m, TipoImporto = TipoVariazionePrezzo.Percentuale }];
        // 20% di 95 € = 19 €, metà per il ragazzo = 9,50 €.
        Assert.Equal(9.5m, PrezziCameraService.SupplementoPerNotte(inPercentuale, meta, 3, [15], 95m));
    }

    [Fact]
    public void FasciaInPercentualeOltre100_Rifiutata()
    {
        Assert.Throws<ConflictException>(() => PrezziCameraService.ValidaFasceEta([new(14, 17, 120m, TipoVariazionePrezzo.Percentuale)]));
    }

    [Fact]
    public void FasceSovrapposte_Rifiutate()
    {
        Assert.Throws<ConflictException>(() => PrezziCameraService.ValidaFasceEta(
            [new FasciaEtaSupplementoDto(0, 14, 0m), new FasciaEtaSupplementoDto(14, 17, 10m)]));
    }

    [Fact]
    public void PiuDiTreFasce_Rifiutate()
    {
        Assert.Throws<ConflictException>(() => PrezziCameraService.ValidaFasceEta(
            [new(0, 2, 0m), new(3, 6, 5m), new(7, 12, 8m), new(13, 17, 10m)]));
    }

    [Theory]
    [InlineData(0, 18)]
    [InlineData(-1, 5)]
    [InlineData(10, 5)]
    public void FasciaFuoriDa0A17_Rifiutata(int min, int max)
    {
        Assert.Throws<ConflictException>(() => PrezziCameraService.ValidaFasceEta([new(min, max, 0m)]));
    }

    [Fact]
    public void FasceDelloStessoEsempio_Accettate()
    {
        PrezziCameraService.ValidaFasceEta([new(0, 13, 0m), new(14, 17, 10m)]);
    }

    [Fact]
    public void Prenotazione_ServeAlmenoUnAdulto()
    {
        Assert.Throws<ConflictException>(() => PrenotazioniService.ValidaEtaBambini([5, 8], numeroOspiti: 2));
    }

    [Fact]
    public void Prenotazione_EtaDiUnMaggiorenneRifiutata()
    {
        Assert.Throws<ConflictException>(() => PrenotazioniService.ValidaEtaBambini([18], numeroOspiti: 3));
    }

    [Fact]
    public void Prenotazione_SenzaBambini_SempreValida()
    {
        Assert.Empty(PrenotazioniService.ValidaEtaBambini([], numeroOspiti: null));
    }
}
