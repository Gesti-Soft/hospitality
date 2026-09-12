using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Termini di legge per le schedine alloggiati: 24 ore dall'arrivo, 6 ore se il soggiorno dura meno
/// di 24 ore. Sono coperti da test perché sbagliarli ha due conseguenze opposte e entrambe costose: una
/// schedina inviata fuori termine viene rifiutata dal portale, una considerata scaduta troppo presto
/// non viene trasmessa affatto — e resta un obbligo di legge non assolto.
/// </summary>
public class TerminiSchedinaTests
{
    private static readonly DateTime Arrivo = new(2026, 9, 12, 15, 0, 0, DateTimeKind.Utc);

    private static Prenotazione Prenotazione(DateTime checkIn, DateTime checkOut, DateTime? arrivoReale) => new()
    {
        CheckIn = checkIn,
        CheckOut = checkOut,
        CheckInEffettuatoAtUtc = arrivoReale,
    };

    [Fact]
    public void SoggiornoConPernottamento_Termine24Ore()
    {
        var prenotazione = Prenotazione(new DateTime(2026, 9, 12), new DateTime(2026, 9, 15), Arrivo);

        Assert.False(TerminiSchedina.IsSoggiornoBreve(prenotazione));
        Assert.Equal(Arrivo.AddHours(24), TerminiSchedina.ScadenzaUtc(prenotazione));
        Assert.True(TerminiSchedina.IsInTermine(prenotazione, Arrivo.AddHours(23)));
        Assert.False(TerminiSchedina.IsInTermine(prenotazione, Arrivo.AddHours(24).AddSeconds(1)));
    }

    [Fact]
    public void SoggiornoNellaStessaGiornata_Termine6Ore()
    {
        var prenotazione = Prenotazione(new DateTime(2026, 9, 12), new DateTime(2026, 9, 12), Arrivo);

        Assert.True(TerminiSchedina.IsSoggiornoBreve(prenotazione));
        Assert.Equal(Arrivo.AddHours(6), TerminiSchedina.ScadenzaUtc(prenotazione));
        Assert.True(TerminiSchedina.IsInTermine(prenotazione, Arrivo.AddHours(5).AddMinutes(59)));
        Assert.False(TerminiSchedina.IsInTermine(prenotazione, Arrivo.AddHours(6).AddSeconds(1)));
    }

    /// <summary>Prenotazioni registrate prima che esistesse l'orario di arrivo: si parte dalla mezzanotte, che anticipa la scadenza invece di posticiparla.</summary>
    [Fact]
    public void SenzaOrarioDiArrivo_SiParteDallaMezzanotteDelCheckIn()
    {
        var prenotazione = Prenotazione(new DateTime(2026, 9, 12), new DateTime(2026, 9, 15), arrivoReale: null);

        Assert.Equal(new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc), TerminiSchedina.ScadenzaUtc(prenotazione));
        Assert.True(TerminiSchedina.IsInTermine(prenotazione, new DateTime(2026, 9, 12, 23, 0, 0, DateTimeKind.Utc)));
        Assert.False(TerminiSchedina.IsInTermine(prenotazione, new DateTime(2026, 9, 13, 1, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>Senza data di arrivo non si può dimostrare di essere in termine: l'invio automatico non deve tentarla.</summary>
    [Fact]
    public void SenzaDataDiArrivo_MaiInTermine()
    {
        var prenotazione = new Prenotazione { CheckIn = null, CheckOut = null };

        Assert.Null(TerminiSchedina.ScadenzaUtc(prenotazione));
        Assert.False(TerminiSchedina.IsInTermine(prenotazione, Arrivo));
    }

    /// <summary>Il termine si conta dall'arrivo reale, non dalla data prevista: un check-in registrato in ritardo non accorcia il tempo disponibile.</summary>
    [Fact]
    public void ArrivoRealeBatteLaDataPrevista()
    {
        var arrivoSerale = new DateTime(2026, 9, 12, 22, 30, 0, DateTimeKind.Utc);
        var prenotazione = Prenotazione(new DateTime(2026, 9, 12), new DateTime(2026, 9, 14), arrivoSerale);

        Assert.Equal(arrivoSerale.AddHours(24), TerminiSchedina.ScadenzaUtc(prenotazione));
        Assert.True(TerminiSchedina.IsInTermine(prenotazione, new DateTime(2026, 9, 13, 20, 0, 0, DateTimeKind.Utc)));
    }
}
