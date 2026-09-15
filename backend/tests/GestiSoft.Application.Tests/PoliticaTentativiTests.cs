using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Limite ai tentativi degli invii giornalieri alle PA. Nasce da una segnalazione dell'utente:
/// il job Osservatorio, che gira ogni minuto dall'orario configurato fino a mezzanotte, produceva
/// centinaia di righe di log identiche a sera — e con molte strutture configurate sarebbero
/// altrettante chiamate al portale.
/// </summary>
public class PoliticaTentativiTests
{
    private static readonly DateTime Adesso = new(2026, 9, 15, 23, 10, 0, DateTimeKind.Utc);

    [Fact]
    public void PuoTentare_PrimoGiroDellaGiornata_Sempre()
    {
        Assert.True(PoliticaTentativi.PuoTentare(0, null, null, false, Adesso));
    }

    /// <summary>Il contatore vale per un giorno solo: l'indomani si ricomincia, anche dopo aver esaurito tutto.</summary>
    [Fact]
    public void PuoTentare_ContatoreDiIeri_Riparte()
    {
        var ieri = Adesso.AddDays(-1);
        Assert.True(PoliticaTentativi.PuoTentare(99, ieri, ieri.AddMinutes(5), true, Adesso));
    }

    [Fact]
    public void PuoTentare_AttesaNonAncoraTrascorsa_Salta()
    {
        Assert.False(PoliticaTentativi.PuoTentare(2, Adesso, Adesso.AddMinutes(3), false, Adesso));
    }

    [Fact]
    public void PuoTentare_AttesaTrascorsa_Riprova()
    {
        Assert.True(PoliticaTentativi.PuoTentare(2, Adesso, Adesso.AddMinutes(-1), false, Adesso));
    }

    [Fact]
    public void PuoTentare_TentativiEsauriti_Salta()
    {
        Assert.False(PoliticaTentativi.PuoTentare(
            PoliticaTentativi.MassimoTentativiRitentabili, Adesso, Adesso.AddMinutes(-1), false, Adesso));
    }

    /// <summary>Credenziali mancanti: un solo giro al giorno, non sei — ritentare stasera non può riuscire.</summary>
    [Fact]
    public void PuoTentare_ErroreDiConfigurazione_UnSoloTentativoAlGiorno()
    {
        Assert.False(PoliticaTentativi.PuoTentare(1, Adesso, Adesso.AddMinutes(-1), true, Adesso));
        Assert.True(PoliticaTentativi.PuoTentare(1, Adesso, Adesso.AddMinutes(-1), false, Adesso));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    [InlineData(6, 32)]
    public void RitardoDopo_Raddoppia(int tentativiFalliti, int minutiAttesi)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutiAttesi), PoliticaTentativi.RitardoDopo(tentativiFalliti));
    }

    /// <summary>La somma dei sei ritardi supera l'ora: copre l'intera finestra tra l'orario di invio e la mezzanotte.</summary>
    [Fact]
    public void RitardoDopo_SeiTentativi_CopronoPiuDiUnOra()
    {
        var totale = Enumerable.Range(1, PoliticaTentativi.MassimoTentativiRitentabili)
            .Aggregate(TimeSpan.Zero, (somma, n) => somma + PoliticaTentativi.RitardoDopo(n));

        Assert.True(totale >= TimeSpan.FromHours(1), $"coperti solo {totale.TotalMinutes} minuti");
    }

    [Fact]
    public void EsauritiDopo_UltimoTentativo_SegnalaUnaVoltaSola()
    {
        Assert.False(PoliticaTentativi.EsauritiDopo(5, erroreDefinitivo: false));
        Assert.True(PoliticaTentativi.EsauritiDopo(6, erroreDefinitivo: false));
        Assert.True(PoliticaTentativi.EsauritiDopo(1, erroreDefinitivo: true));
    }
}
