using GestiSoft.Application.Notifiche;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>Dove porta il clic su una notifica: le chiavi sono scritte come le scrivono i servizi che le creano.</summary>
public class CollegamentoNotificaTests
{
    private static string? Collegamento(TipoNotifica tipo, string? chiave = null, Guid? prenotazioneId = null) =>
        CollegamentoNotifica.Per(new Notifica { Tipo = tipo, ChiaveDedup = chiave, PrenotazioneId = prenotazioneId });

    [Fact]
    public void Prenotazione_ApreLaPrenotazione()
    {
        var id = Guid.NewGuid();
        Assert.Equal($"/calendario?prenotazione={id}", Collegamento(TipoNotifica.NuovaPrenotazione, prenotazioneId: id));
        Assert.Equal($"/calendario?prenotazione={id}", Collegamento(TipoNotifica.PrenotazioneAnnullata, prenotazioneId: id));
    }

    [Theory]
    [InlineData("invio-non-riuscito:AlloggiatiWeb:0b3c2d8e-0000-0000-0000-000000000001:20261015", "/polizia-di-stato")]
    [InlineData("invio-non-riuscito:Osservatorio Turistico:Casa Blu:20261015", "/osservatorio")]
    [InlineData("invio-non-riuscito:PayTourist:Castellammare:20261015", "/paytourist")]
    public void InvioNonRiuscito_ApreLaPaginaDelServizio(string chiave, string atteso) =>
        Assert.Equal(atteso, Collegamento(TipoNotifica.InvioSchedineNonRiuscito, chiave));

    [Fact]
    public void Fatturazione_ApreFattureOFattureRicevute()
    {
        Assert.Equal("/fatturazione", Collegamento(TipoNotifica.EsitoFatturaSdi, "sdi:esito:IT01234567890_01000.xml:Consegna"));
        Assert.Equal("/fatturazione?tab=fatture-ricevute", Collegamento(TipoNotifica.FatturaRicevuta, "sdi:ricevuta:32715652"));
    }

    [Fact]
    public void TicketChiuso_ApreIlTicket()
    {
        var id = Guid.NewGuid();
        Assert.Equal($"/assistenza?ticket={id}", Collegamento(TipoNotifica.TicketChiuso, $"ticket-chiuso:{id}"));
    }

    [Theory]
    [InlineData(TipoNotifica.LicenzaInScadenza)]
    [InlineData(TipoNotifica.LicenzaScaduta)]
    [InlineData(TipoNotifica.SchedineInviate)]
    [InlineData(TipoNotifica.GestionaleAggiornato)]
    public void SoloInformative_NessunCollegamento(TipoNotifica tipo) => Assert.Null(Collegamento(tipo, "qualcosa:1"));
}
