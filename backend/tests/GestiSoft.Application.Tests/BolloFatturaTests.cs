using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// IVA e bollo sono alternativi (art. 6 Tabella B DPR 642/72): dove c'è IVA il bollo non si paga,
/// dove non c'è si paga sopra 77,47 €. Sbagliare in un verso significa far pagare 2 € di troppo a
/// ogni fattura, nell'altro accumulare un debito che l'Agenzia calcola da sola sui dati dello SDI e
/// chiede dopo. La soglia si misura sulla sola parte non soggetta, non sul totale del documento.
/// </summary>
public class BolloFatturaTests
{
    /// <summary>Hotel in regime ordinario: soggiorno con IVA al 10%, nessun bollo per quanto alto sia l'importo.</summary>
    [Fact]
    public void FatturaConIva_NonPagaMaiIlBollo()
    {
        Assert.Null(FatturazioneService.CalcolaBollo(TipoEmissioneDocumento.Fattura, natura: null, prezzoTotale: 5000m, impostaSoggiorno: 0m));
    }

    /// <summary>Forfettario: l'intero importo è fuori dall'IVA, quindi sopra soglia il bollo c'è.</summary>
    [Fact]
    public void FatturaSenzaIvaSopraSoglia_PagaDueEuro()
    {
        Assert.Equal(2.00m, FatturazioneService.CalcolaBollo(TipoEmissioneDocumento.Fattura, NaturaIva.N2_2_NonSoggetteAltriCasi, 200m, 0m));
    }

    [Theory]
    [InlineData(77.47)]
    [InlineData(50)]
    public void FatturaSenzaIvaFinoAllaSoglia_NonPagaNulla(decimal importo)
    {
        Assert.Null(FatturazioneService.CalcolaBollo(TipoEmissioneDocumento.Fattura, NaturaIva.N2_2_NonSoggetteAltriCasi, importo, 0m));
    }

    [Fact]
    public void SopraLaSogliaDiUnCentesimo_PagaIlBollo()
    {
        Assert.Equal(2.00m, FatturazioneService.CalcolaBollo(TipoEmissioneDocumento.Fattura, NaturaIva.N2_2_NonSoggetteAltriCasi, 77.48m, 0m));
    }

    /// <summary>
    /// Fattura mista: il soggiorno con IVA non conta per la soglia, conta solo l'imposta di
    /// soggiorno esclusa art. 15. Un'imposta di soggiorno normale non ci arriva mai — ed è il motivo
    /// per cui un hotel ordinario, in pratica, il bollo non lo paga.
    /// </summary>
    [Fact]
    public void FatturaMista_LaSogliaSiMisuraSoloSullaParteSenzaIva()
    {
        Assert.Null(FatturazioneService.CalcolaBollo(TipoEmissioneDocumento.Fattura, natura: null, prezzoTotale: 1000m, impostaSoggiorno: 12m));
    }

    /// <summary>Soggiorno lungo di gruppo: qui anche la sola imposta di soggiorno supera la soglia.</summary>
    [Fact]
    public void FatturaMista_ImpostaDiSoggiornoSopraSoglia_PagaIlBollo()
    {
        Assert.Equal(2.00m, FatturazioneService.CalcolaBollo(TipoEmissioneDocumento.Fattura, natura: null, prezzoTotale: 1000m, impostaSoggiorno: 90m));
    }

    /// <summary>
    /// Ricevuta di locazione breve: nessuna natura da esporre — non essendoci fattura elettronica non
    /// c'è un codice da scrivere — ma l'operazione è interamente fuori dal campo IVA, quindi
    /// l'importo intero conta per la soglia. È il caso più comune di tutti: un affitto di qualche
    /// notte supera sempre 77,47 €.
    /// </summary>
    [Fact]
    public void RicevutaDiLocazioneBreve_PagaIlBolloSullImportoIntero()
    {
        Assert.Equal(2.00m, FatturazioneService.CalcolaBollo(TipoEmissioneDocumento.Ricevuta, natura: null, prezzoTotale: 350m, impostaSoggiorno: 0m));
    }

    [Fact]
    public void RicevutaSottoSoglia_NonPagaNulla()
    {
        Assert.Null(FatturazioneService.CalcolaBollo(TipoEmissioneDocumento.Ricevuta, natura: null, prezzoTotale: 60m, impostaSoggiorno: 0m));
    }

    /// <summary>Forfettario con imposta di soggiorno: sommano entrambe, perché nessuna delle due è soggetta a IVA.</summary>
    [Fact]
    public void ForfettarioConImpostaDiSoggiorno_SommaLeDueParti()
    {
        Assert.Equal(2.00m, FatturazioneService.CalcolaBollo(TipoEmissioneDocumento.Fattura, NaturaIva.N2_2_NonSoggetteAltriCasi, 70m, 10m));
    }
}
