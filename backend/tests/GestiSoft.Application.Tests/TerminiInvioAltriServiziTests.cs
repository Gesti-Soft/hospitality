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
    private static readonly DateTime Oggi = new(2026, 8, 9);

    /// <summary>Esempio dato dall'utente: schedina dell'08/08, appartamento chiuso fino al 09/08 → non va inviata.</summary>
    [Fact]
    public void Osservatorio_ArrivoAnterioreAllaChiusura_NonTrasmissibile()
    {
        Assert.False(TerminiOsservatorio.IsInTermine(new DateTime(2026, 8, 8), cursore: new DateTime(2026, 8, 9), Oggi));
    }

    /// <summary>Si trasmette solo la giornata che sta per essere chiusa, non quelle successive.</summary>
    [Fact]
    public void Osservatorio_SoloIlGiornoDaChiudere_Trasmissibile()
    {
        var cursore = new DateTime(2026, 8, 9);

        Assert.True(TerminiOsservatorio.IsInTermine(new DateTime(2026, 8, 9), cursore, Oggi));
        Assert.False(TerminiOsservatorio.IsInTermine(new DateTime(2026, 8, 10), cursore, Oggi));
    }

    /// <summary>
    /// Appartamento mai chiuso: valgono solo gli arrivi odierni. Prima si consideravano trasmissibili
    /// gli arrivi di qualsiasi giorno passato, ed è il motivo per cui il pulsante "Invia" compariva
    /// anche su schedine vecchie (segnalato dall'utente).
    /// </summary>
    [Fact]
    public void Osservatorio_SenzaCursore_SoloGliArriviDiOggi()
    {
        Assert.True(TerminiOsservatorio.IsInTermine(Oggi, cursore: null, Oggi));
        Assert.False(TerminiOsservatorio.IsInTermine(Oggi.AddDays(-1), cursore: null, Oggi));
        Assert.False(TerminiOsservatorio.IsInTermine(Oggi.AddDays(-30), cursore: null, Oggi));
        Assert.False(TerminiOsservatorio.IsInTermine(checkIn: null, cursore: null, Oggi));
    }

    /// <summary>Giornate arretrate da chiudere: a mano non si trasmette nulla, le chiude solo il job automatico.</summary>
    [Fact]
    public void Osservatorio_ConArretrato_NienteDiTrasmissibile()
    {
        var cursoreIndietro = Oggi.AddDays(-3);

        Assert.Null(TerminiOsservatorio.GiornoTrasmissibile(cursoreIndietro, Oggi));
        Assert.False(TerminiOsservatorio.IsInTermine(cursoreIndietro, cursoreIndietro, Oggi));
        Assert.False(TerminiOsservatorio.IsInTermine(Oggi, cursoreIndietro, Oggi));
    }

    /// <summary>Giornata odierna già chiusa (cursore a domani): per oggi non c'è più niente da inviare.</summary>
    [Fact]
    public void Osservatorio_OggiGiaChiuso_NienteDaInviare()
    {
        Assert.False(TerminiOsservatorio.IsInTermine(Oggi, cursore: Oggi.AddDays(1), Oggi));
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
