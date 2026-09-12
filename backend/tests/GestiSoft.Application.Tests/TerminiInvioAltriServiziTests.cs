using GestiSoft.Application.Osservatorio;
using GestiSoft.Application.PayTourist;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Finestre di trasmissione degli altri due servizi, che seguono regole diverse da quelle della
/// Polizia di Stato (vedi TerminiSchedinaTests): l'Osservatorio Turistico ragiona per giornate
/// chiuse, PayTourist per giorni dal check-out.
/// </summary>
public class TerminiInvioAltriServiziTests
{
    /// <summary>Esempio dato dall'utente: schedina dell'08/08, appartamento chiuso fino al 09/08 → non va inviata.</summary>
    [Fact]
    public void Osservatorio_ArrivoAnterioreAllaChiusura_NonTrasmissibile()
    {
        var arrivo = new DateTime(2026, 8, 8);
        var cursore = new DateTime(2026, 8, 9);

        Assert.False(TerminiOsservatorio.IsInTermine(arrivo, cursore));
    }

    [Fact]
    public void Osservatorio_ArrivoDelGiornoDaChiudereODopo_Trasmissibile()
    {
        var cursore = new DateTime(2026, 8, 9);

        Assert.True(TerminiOsservatorio.IsInTermine(new DateTime(2026, 8, 9), cursore));
        Assert.True(TerminiOsservatorio.IsInTermine(new DateTime(2026, 8, 10), cursore));
    }

    /// <summary>Appartamento mai chiuso: nessuna giornata è ancora andata, quindi non c'è niente da escludere.</summary>
    [Fact]
    public void Osservatorio_SenzaCursore_TuttoTrasmissibile()
    {
        Assert.True(TerminiOsservatorio.IsInTermine(new DateTime(2026, 1, 1), cursore: null));
        Assert.False(TerminiOsservatorio.IsInTermine(checkIn: null, cursore: null));
    }

    [Fact]
    public void PayTourist_EntroSetteGiorniDalCheckOut_Trasmissibile()
    {
        var checkOut = new DateTime(2026, 9, 1);

        Assert.Equal(new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc), TerminiPayTourist.ScadenzaUtc(checkOut));
        Assert.True(TerminiPayTourist.IsInTermine(checkOut, new DateTime(2026, 9, 1, 23, 0, 0, DateTimeKind.Utc)));
        Assert.True(TerminiPayTourist.IsInTermine(checkOut, new DateTime(2026, 9, 8, 23, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void PayTourist_OltreSetteGiorni_Escluso()
    {
        var checkOut = new DateTime(2026, 9, 1);

        Assert.False(TerminiPayTourist.IsInTermine(checkOut, new DateTime(2026, 9, 9, 0, 1, 0, DateTimeKind.Utc)));
        Assert.False(TerminiPayTourist.IsInTermine(checkOut: null, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc)));
    }
}
