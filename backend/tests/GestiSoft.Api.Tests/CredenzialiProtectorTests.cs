using System.Security.Cryptography;
using GestiSoft.Infrastructure.Security;

namespace GestiSoft.Api.Tests;

/// <summary>
/// La cifratura delle credenziali è silenziosa: se si rompesse, nessuno se ne accorgerebbe finché
/// un invio a un portale della PA non venisse rifiutato per credenziali sbagliate — o, peggio,
/// finché un token non tornasse a essere scritto in chiaro senza che nessuno lo noti.
/// </summary>
public class CredenzialiProtectorTests
{
    private static CredenzialiProtector Protector(byte seme = 1) =>
        new(Enumerable.Repeat(seme, 32).ToArray());

    [Fact]
    public void ValoreProtetto_SiRilegge_Identico()
    {
        var protector = Protector();
        const string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.token-di-prova";

        var protetto = protector.Proteggi(token);

        Assert.NotEqual(token, protetto);
        Assert.StartsWith(CredenzialiProtector.Prefisso, protetto);
        Assert.Equal(token, protector.Leggi(protetto));
    }

    /// <summary>Nonce casuale ad ogni scrittura: due cifrature dello stesso token non devono coincidere, o dal database si capirebbe che due strutture usano la stessa credenziale.</summary>
    [Fact]
    public void StessoValore_CifratoDueVolte_DaRisultatiDiversi()
    {
        var protector = Protector();

        Assert.NotEqual(protector.Proteggi("stesso-token"), protector.Proteggi("stesso-token"));
    }

    /// <summary>I valori scritti prima che la cifratura esistesse non hanno il prefisso: vanno restituiti così come sono, altrimenti ogni struttura già configurata smetterebbe di funzionare al primo deploy.</summary>
    [Fact]
    public void ValoreStoricoInChiaro_SiLeggeCosiComE()
    {
        Assert.Equal("token-vecchio-in-chiaro", Protector().Leggi("token-vecchio-in-chiaro"));
    }

    [Fact]
    public void ValoreGiaProtetto_NonVieneCifratoDueVolte()
    {
        var protector = Protector();
        var protetto = protector.Proteggi("token");

        Assert.Equal(protetto, protector.Proteggi(protetto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValoreVuoto_RestaVuoto(string? valore)
    {
        var protector = Protector();

        Assert.Equal(valore, protector.Proteggi(valore));
        Assert.Equal(valore, protector.Leggi(valore));
    }

    /// <summary>Con la chiave sbagliata si deve fallire, non restituire spazzatura: una credenziale letta male produrrebbe rifiuti dal portale senza una ragione comprensibile.</summary>
    [Fact]
    public void ChiaveDiversa_NonRiesceALeggere()
    {
        var protetto = Protector(seme: 1).Proteggi("token");

        Assert.Throws<InvalidOperationException>(() => Protector(seme: 2).Leggi(protetto));
    }

    /// <summary>Il tag di autenticazione di AES-GCM deve far fallire la lettura di un valore manomesso.</summary>
    [Fact]
    public void ValoreManomesso_NonRiesceALeggere()
    {
        var protector = Protector();
        var protetto = protector.Proteggi("token")!;

        var pacchetto = Convert.FromBase64String(protetto[CredenzialiProtector.Prefisso.Length..]);
        pacchetto[^1] ^= 0xFF;
        var manomesso = CredenzialiProtector.Prefisso + Convert.ToBase64String(pacchetto);

        Assert.Throws<InvalidOperationException>(() => protector.Leggi(manomesso));
    }

    /// <summary>
    /// Durante una rotazione convivono valori scritti con la chiave vecchia e con la nuova: se non
    /// si leggessero entrambi, cambiare chiave significherebbe reinserire a mano tutte le
    /// credenziali di tutte le strutture.
    /// </summary>
    [Fact]
    public void DuranteLaRotazione_SiLeggeAncheCioCheHaLaChiaveVecchia()
    {
        var vecchio = Protector(seme: 1).Proteggi("token-di-ieri");
        var inRotazione = new CredenzialiProtector(Enumerable.Repeat((byte)2, 32).ToArray(), Enumerable.Repeat((byte)1, 32).ToArray());

        Assert.Equal("token-di-ieri", inRotazione.Leggi(vecchio));
        Assert.Equal("token-di-oggi", inRotazione.Leggi(inRotazione.Proteggi("token-di-oggi")));
    }

    /// <summary>La scrittura deve usare sempre la chiave corrente, o la rotazione non finirebbe mai: quello che riscrive non deve più essere leggibile con la sola chiave vecchia.</summary>
    [Fact]
    public void DuranteLaRotazione_SiScriveSempreConLaChiaveNuova()
    {
        var inRotazione = new CredenzialiProtector(Enumerable.Repeat((byte)2, 32).ToArray(), Enumerable.Repeat((byte)1, 32).ToArray());

        var riscritto = inRotazione.Proteggi("token");

        Assert.Equal("token", Protector(seme: 2).Leggi(riscritto));
        Assert.Throws<InvalidOperationException>(() => Protector(seme: 1).Leggi(riscritto));
    }

    [Fact]
    public void SenzaChiavePrecedente_NonRisultaNessunaRotazione()
    {
        Assert.False(Protector().RotazioneInCorso);
        Assert.True(new CredenzialiProtector(Enumerable.Repeat((byte)2, 32).ToArray(), Enumerable.Repeat((byte)1, 32).ToArray()).RotazioneInCorso);
    }

    [Fact]
    public void ChiavePrecedenteDiLunghezzaSbagliata_VieneRifiutata()
    {
        Assert.Throws<InvalidOperationException>(
            () => new CredenzialiProtector(Enumerable.Repeat((byte)1, 32).ToArray(), RandomNumberGenerator.GetBytes(8)));
    }

    [Fact]
    public void ChiaveDiLunghezzaSbagliata_VieneRifiutata()
    {
        Assert.Throws<InvalidOperationException>(() => new CredenzialiProtector(RandomNumberGenerator.GetBytes(16)));
    }
}
