using GestiSoft.Application.Pulizie;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Pulizia e cambio biancheria durante il soggiorno. Un errore qui si vede subito al piano: una
/// camera che nessuno rifà per una settimana, o un addetto mandato a bussare il giorno della partenza.
/// Esempio di riferimento: soggiorno dal 10 al 17, pulizia ogni 3 giorni.
/// </summary>
public class CalcoloServizioSoggiornoTests
{
    private static readonly DateTime Arrivo = new(2026, 9, 10);
    private static readonly DateTime Partenza = new(2026, 9, 17);

    private static EsitoServizioSoggiorno Calcola(DateTime oggi, DateTime? ultimaFatta = null, int? intervallo = 3, bool rinunciato = false) =>
        CalcoloServizioSoggiorno.Calcola(Arrivo, Partenza, ultimaFatta, intervallo, rinunciato, oggi);

    [Fact]
    public void PrimaPulizia_TreGiorniDopoLArrivo()
    {
        var esito = Calcola(oggi: new DateTime(2026, 9, 13));

        Assert.Equal(StatoServizioSoggiorno.DaFareOggi, esito.Stato);
        Assert.Equal(new DateTime(2026, 9, 13), esito.Previsto);
    }

    /// <summary>Il giorno dell'arrivo la camera è appena stata preparata: niente pulizia.</summary>
    [Fact]
    public void GiornoDellArrivo_Programmata()
    {
        var esito = Calcola(oggi: Arrivo);

        Assert.Equal(StatoServizioSoggiorno.Programmato, esito.Stato);
        Assert.Equal(new DateTime(2026, 9, 13), esito.Previsto);
    }

    [Fact]
    public void PrevistaIeriENonFatta_InRitardo()
    {
        Assert.Equal(StatoServizioSoggiorno.InRitardo, Calcola(oggi: new DateTime(2026, 9, 14)).Stato);
    }

    /// <summary>Saltata il 13 e fatta il 14: la successiva si conta dal 14, non dal 13.</summary>
    [Fact]
    public void PuliziaInRitardo_SpostaLeSuccessive()
    {
        var esito = Calcola(oggi: new DateTime(2026, 9, 15), ultimaFatta: new DateTime(2026, 9, 14));

        Assert.Equal(StatoServizioSoggiorno.NonPrevisto, esito.Stato);
        Assert.Null(esito.Previsto);
    }

    /// <summary>Fatta il 13, la successiva cadrebbe il 16: il giorno prima della partenza, quindi prevista.</summary>
    [Fact]
    public void SecondaPulizia_PrimaDellaPartenza()
    {
        var esito = Calcola(oggi: new DateTime(2026, 9, 16), ultimaFatta: new DateTime(2026, 9, 13));

        Assert.Equal(StatoServizioSoggiorno.DaFareOggi, esito.Stato);
    }

    /// <summary>Il giorno della partenza c'è la pulizia del check-out, non una intermedia.</summary>
    [Fact]
    public void PrevistaIlGiornoDellaPartenza_NonPrevista()
    {
        var esito = CalcoloServizioSoggiorno.Calcola(Arrivo, new DateTime(2026, 9, 13), null, 3, false, new DateTime(2026, 9, 12));

        Assert.Equal(StatoServizioSoggiorno.NonPrevisto, esito.Stato);
    }

    [Fact]
    public void SoggiornoPiuCortoDellIntervallo_NonPrevista()
    {
        var esito = CalcoloServizioSoggiorno.Calcola(Arrivo, new DateTime(2026, 9, 12), null, 3, false, new DateTime(2026, 9, 11));

        Assert.Equal(StatoServizioSoggiorno.NonPrevisto, esito.Stato);
    }

    [Fact]
    public void RinunciaDellOspite_VinceSullaRegola()
    {
        var esito = Calcola(oggi: new DateTime(2026, 9, 13), rinunciato: true);

        Assert.Equal(StatoServizioSoggiorno.Rinunciato, esito.Stato);
    }

    [Fact]
    public void SenzaIntervallo_NonPrevista()
    {
        Assert.Equal(StatoServizioSoggiorno.NonPrevisto, Calcola(oggi: new DateTime(2026, 9, 13), intervallo: null).Stato);
    }

    [Fact]
    public void PuliziaQuotidiana_OgniGiornoTranneArrivoEPartenza()
    {
        Assert.Equal(StatoServizioSoggiorno.Programmato, Calcola(oggi: Arrivo, intervallo: 1).Stato);
        Assert.Equal(StatoServizioSoggiorno.DaFareOggi, Calcola(oggi: new DateTime(2026, 9, 11), intervallo: 1).Stato);
        Assert.Equal(StatoServizioSoggiorno.DaFareOggi, Calcola(oggi: new DateTime(2026, 9, 16), ultimaFatta: new DateTime(2026, 9, 15), intervallo: 1).Stato);
        Assert.Equal(StatoServizioSoggiorno.NonPrevisto, Calcola(oggi: new DateTime(2026, 9, 16), ultimaFatta: new DateTime(2026, 9, 16), intervallo: 1).Stato);
    }

    [Theory]
    [InlineData(7, 3, 7)] // la tipologia vince sulla struttura
    [InlineData(null, 3, 3)] // tipologia vuota: segue la struttura
    [InlineData(0, 3, null)] // tipologia 0: nessuna, anche se la struttura la prevede
    [InlineData(null, null, null)] // nessuno dei due: nessuna
    public void Gerarchia_TipologiaPoiStruttura(int? tipologia, int? struttura, int? atteso)
    {
        Assert.Equal(atteso, CalcoloServizioSoggiorno.IntervalloEffettivo(tipologia, struttura));
    }
}
