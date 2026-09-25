using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Trattamenti;
using GestiSoft.Contracts.Trattamenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Trattamenti a persona e a notte: gli adulti pagano il prezzo pieno, i bambini fino all'età
/// massima il prezzo bambini se c'è. Esempio: mezza pensione 35 €, bambini fino a 11 anni al 50%.
/// </summary>
public class TrattamentiTests
{
    private static readonly TrattamentoStruttura MezzaPensione = new()
    {
        Tipo = TipoTrattamento.MezzaPensione,
        PrezzoPerPersona = 35m,
        PrezzoBambini = 50m,
        TipoPrezzoBambini = TipoVariazionePrezzo.Percentuale,
        EtaMassimaBambini = 11,
    };

    [Fact]
    public void PrezzoBambiniInPercentuale_SiCopiaInEuro()
    {
        var prezzi = TrattamentiService.PrezziDa(MezzaPensione);
        Assert.Equal(35m, prezzi.PrezzoAdulto);
        Assert.Equal(17.5m, prezzi.PrezzoBambino);
        Assert.Equal(11, prezzi.EtaMassimaBambini);
    }

    [Fact]
    public void DueAdulti_PaganoEntrambiPieno()
    {
        Assert.Equal(70m, TrattamentiService.ImportoPerNotte(TrattamentiService.PrezziDa(MezzaPensione), 2, []));
    }

    [Fact]
    public void DueAdultiEUnBambinoDi8Anni_IlBambinoPagaRidotto()
    {
        Assert.Equal(87.5m, TrattamentiService.ImportoPerNotte(TrattamentiService.PrezziDa(MezzaPensione), 3, [8]));
    }

    [Fact]
    public void RagazzoOltreEtaMassima_PagaComeUnAdulto()
    {
        Assert.Equal(105m, TrattamentiService.ImportoPerNotte(TrattamentiService.PrezziDa(MezzaPensione), 3, [14]));
    }

    [Fact]
    public void SenzaPrezzoBambini_TuttiPaganoPieno()
    {
        var colazione = new TrattamentoStruttura { Tipo = TipoTrattamento.Colazione, PrezzoPerPersona = 10m };
        Assert.Equal(30m, TrattamentiService.ImportoPerNotte(TrattamentiService.PrezziDa(colazione), 3, [4]));
    }

    [Fact]
    public void NeonatoGratis_ConPrezzoBambiniAZero()
    {
        var colazione = new TrattamentoStruttura { Tipo = TipoTrattamento.Colazione, PrezzoPerPersona = 10m, PrezzoBambini = 0m, EtaMassimaBambini = 2 };
        Assert.Equal(20m, TrattamentiService.ImportoPerNotte(TrattamentiService.PrezziDa(colazione), 3, [1]));
    }

    [Fact]
    public void PrezzoBambiniSenzaEta_Rifiutato()
    {
        Assert.Throws<ConflictException>(() => TrattamentiService.Valida(
            new TrattamentoStrutturaDto(TipoTrattamento.Colazione, true, 10m, 5m, TipoVariazionePrezzo.Euro, null, null)));
    }

    [Fact]
    public void PrezzoBambiniOltre100Percento_Rifiutato()
    {
        Assert.Throws<ConflictException>(() => TrattamentiService.Valida(
            new TrattamentoStrutturaDto(TipoTrattamento.Colazione, true, 10m, 120m, TipoVariazionePrezzo.Percentuale, 11, null)));
    }

    [Fact]
    public void PrenotazioneSoloPernottamento_NessunPrezzo()
    {
        Assert.Null(TrattamentiService.PrezziDellaPrenotazione(new Prenotazione()));
    }
}
