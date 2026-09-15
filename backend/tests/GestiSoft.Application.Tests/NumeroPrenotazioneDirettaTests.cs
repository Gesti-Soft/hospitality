using GestiSoft.Application.Prenotazioni;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Copre il bug reale segnalato dall'utente: modificando due prenotazioni dalla sezione Arrivi
/// (togliendo la tassa di soggiorno) il numero prenotazione veniva rigenerato, e a entrambe veniva
/// assegnato lo stesso. Due cause: il salvataggio riassegnava un numero già dato (corretto in
/// PrenotazioniService.AggiornaAsync, che ora passa il numero esistente e lo conserva) e il
/// generatore contava le dirette dell'anno invece di guardare l'ultimo progressivo assegnato —
/// contando, una prenotazione già esistente e già contata riotteneva sempre lo stesso "totale + 1".
/// </summary>
public class NumeroPrenotazioneDirettaTests
{
    [Fact]
    public void ProssimoNumeroDiretta_NessunaPrenotazione_ParteDa1()
    {
        Assert.Equal("1", PrenotazioniService.ProssimoNumeroDiretta([]));
    }

    [Fact]
    public void ProssimoNumeroDiretta_SequenzaPiena_ContinuaDallUltimo()
    {
        Assert.Equal("4", PrenotazioniService.ProssimoNumeroDiretta(["1", "2", "3"]));
    }

    /// <summary>
    /// Il caso che il conteggio sbagliava: tre numeri assegnati ma il più alto è 7 (le altre
    /// dirette dell'anno sono passate a un altro canale). Contando avrebbe restituito "4",
    /// già in uso.
    /// </summary>
    [Fact]
    public void ProssimoNumeroDiretta_SequenzaConBuchi_NonRiusaUnNumeroGiaDato()
    {
        Assert.Equal("8", PrenotazioniService.ProssimoNumeroDiretta(["1", "5", "7"]));
    }

    [Fact]
    public void ProssimoNumeroDiretta_NumeriNonNumericiONulli_Ignorati()
    {
        Assert.Equal("3", PrenotazioniService.ProssimoNumeroDiretta([null, "", "ABC-99", "2"]));
    }

    [Fact]
    public void ProssimoNumeroDiretta_SoloNumeriNonValidi_ParteDa1()
    {
        Assert.Equal("1", PrenotazioniService.ProssimoNumeroDiretta([null, "codice-esterno"]));
    }
}
