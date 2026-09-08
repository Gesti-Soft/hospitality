using GestiSoft.Infrastructure.Repositories;

namespace GestiSoft.Api.Tests;

/// <summary>
/// Verifica il parsing "08/05" -> intervallo UTC di quel giorno di calendario italiano, usato dalla
/// ricerca testuale del log (richiesta esplicita dell'utente: la stessa casella di ricerca deve
/// trovare gli eventi di una data, non solo per messaggio/operatore/correlation id).
/// </summary>
public class LogEventoRepositoryDataSearchTests
{
    [Theory]
    [InlineData("08/05")]
    [InlineData("08-05")]
    [InlineData("08.05")]
    [InlineData(" 08/05 ")]
    public void Giorno_mese_senza_anno_usa_l_anno_corrente(string testo)
    {
        var trovato = LogEventoRepository.ProvaEstraiIntervalloData(testo, out var inizioUtc, out var fineUtc);

        // Conversione esplicita in ora italiana, non ToLocalTime() (dipenderebbe dal fuso orario
        // della macchina che esegue il test, non da quello — sempre Europe/Rome — usato dal metodo).
        var annoLocaleAtteso = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")).Year;
        var annoLocaleInizio = TimeZoneInfo.ConvertTimeFromUtc(inizioUtc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")).Year;

        Assert.True(trovato);
        Assert.Equal(annoLocaleAtteso, annoLocaleInizio);
        Assert.Equal(TimeSpan.FromDays(1), fineUtc - inizioUtc);
    }

    [Theory]
    [InlineData("8")]
    [InlineData("08")]
    public void Solo_il_giorno_usa_mese_e_anno_correnti(string testo)
    {
        // Bug segnalato dall'utente: cercando solo "08" (senza mese) non trovava gli eventi
        // dell'8 del mese corrente, perché il pattern richiedeva sempre almeno giorno/mese.
        var trovato = LogEventoRepository.ProvaEstraiIntervalloData(testo, out var inizioUtc, out var fineUtc);

        var fuso = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        var oggiLocale = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso);
        var atteso = TimeZoneInfo.ConvertTimeToUtc(new DateTime(oggiLocale.Year, oggiLocale.Month, 8, 0, 0, 0, DateTimeKind.Unspecified), fuso);

        Assert.True(trovato);
        Assert.Equal(atteso, inizioUtc);
        Assert.Equal(TimeSpan.FromDays(1), fineUtc - inizioUtc);
    }

    [Theory]
    [InlineData("08/05/2026")]
    [InlineData("08/05/26")]
    public void Con_anno_esplicito_ritorna_lo_stesso_intervallo_a_prescindere_dal_formato(string testo)
    {
        LogEventoRepository.ProvaEstraiIntervalloData("08/05/2026", out var inizioAtteso, out var fineAtteso);
        var trovato = LogEventoRepository.ProvaEstraiIntervalloData(testo, out var inizioUtc, out var fineUtc);

        Assert.True(trovato);
        Assert.Equal(inizioAtteso, inizioUtc);
        Assert.Equal(fineAtteso, fineUtc);
    }

    [Theory]
    [InlineData("32/13")] // giorno e mese entrambi fuori range
    [InlineData("31/02")] // 31 febbraio non esiste
    [InlineData("osservatorio")] // testo libero, non una data
    [InlineData("")]
    [InlineData("12/2026")] // un solo separatore: ambiguo (mese/anno?), non un giorno/mese valido
    [InlineData("00")] // nessun mese ha un giorno 0
    [InlineData("45")] // nessun mese ha un giorno 45
    public void Testo_non_riconoscibile_come_data_non_trova_nulla_ne_lancia(string testo)
    {
        var trovato = LogEventoRepository.ProvaEstraiIntervalloData(testo, out _, out _);

        Assert.False(trovato);
    }

    [Fact]
    public void L_intervallo_copre_esattamente_il_giorno_locale_italiano_non_UTC()
    {
        // 8 maggio è in ora legale (CEST, UTC+2): mezzanotte locale = 22:00 UTC del giorno prima.
        LogEventoRepository.ProvaEstraiIntervalloData("08/05/2026", out var inizioUtc, out var fineUtc);

        Assert.Equal(new DateTime(2026, 5, 7, 22, 0, 0, DateTimeKind.Utc), inizioUtc);
        Assert.Equal(new DateTime(2026, 5, 8, 22, 0, 0, DateTimeKind.Utc), fineUtc);
    }
}
