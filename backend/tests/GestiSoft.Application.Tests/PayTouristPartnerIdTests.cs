using GestiSoft.Application.PayTourist;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Il "partner_id" è la chiave con cui PayTourist riconosce una prenotazione già dichiarata, e su
/// cui PayTouristInvioService decide se ritrasmetterla o saltarla. Cambiarne il formato non
/// romperebbe nulla in modo visibile: l'invio continuerebbe a funzionare, ma il confronto con
/// l'elenco del portale smetterebbe di trovare le corrispondenze e ogni prenotazione verrebbe
/// dichiarata una seconda volta — cioè imposta di soggiorno chiesta due volte allo stesso ospite,
/// senza nessun endpoint per annullarla. Da qui il test.
/// </summary>
public class PayTouristPartnerIdTests
{
    /// <summary>
    /// Valore osservato dal vivo: prenotazione dichiarata sull'ambiente di test PayTourist e riletta
    /// con GET api/v1/reservations, che l'ha restituita con questo identico partner_id.
    /// </summary>
    [Fact]
    public void PartnerId_HaIlFormatoCheIlPortaleRestituisce()
    {
        var prenotazione = new Prenotazione
        {
            Id = Guid.Parse("c1afdf8c-c82f-40be-bfc5-97a8fcadfb2d"),
            CheckIn = new DateTime(2026, 9, 1),
        };

        Assert.Equal(
            "reservation_c1afdf8c-c82f-40be-bfc5-97a8fcadfb2d_2026-09-01",
            PayTouristDtoBuilder.PartnerIdPrenotazione(prenotazione));
    }

    /// <summary>L'orario del check-in non deve entrare nella chiave: la stessa prenotazione registrata a mezzanotte o alle 15 resta la stessa prenotazione.</summary>
    [Fact]
    public void PartnerId_IgnoraLOrarioDelCheckIn()
    {
        var id = Guid.NewGuid();

        var aMezzanotte = new Prenotazione { Id = id, CheckIn = new DateTime(2026, 9, 1, 0, 0, 0) };
        var nelPomeriggio = new Prenotazione { Id = id, CheckIn = new DateTime(2026, 9, 1, 15, 30, 0) };

        Assert.Equal(
            PayTouristDtoBuilder.PartnerIdPrenotazione(aMezzanotte),
            PayTouristDtoBuilder.PartnerIdPrenotazione(nelPomeriggio));
    }

    /// <summary>Prenotazioni diverse con lo stesso arrivo devono restare distinte, altrimenti il controllo ne scarterebbe una scambiandola per l'altra.</summary>
    [Fact]
    public void PartnerId_DistinguePrenotazioniDiverseNelloStessoGiorno()
    {
        var checkIn = new DateTime(2026, 9, 1);

        var prima = new Prenotazione { Id = Guid.NewGuid(), CheckIn = checkIn };
        var seconda = new Prenotazione { Id = Guid.NewGuid(), CheckIn = checkIn };

        Assert.NotEqual(
            PayTouristDtoBuilder.PartnerIdPrenotazione(prima),
            PayTouristDtoBuilder.PartnerIdPrenotazione(seconda));
    }

    private static readonly DateTime Nascita = new(1954, 5, 8);

    private static readonly DateTime Arrivo = new(2026, 9, 1);

    /// <summary>
    /// Il portale restituisce nome e cognome in un campo unico e senza un ordine garantito: il
    /// confronto deve riconoscere la stessa persona comunque siano disposti, altrimenti l'ospite
    /// caricato a mano col file di Pubblica Sicurezza verrebbe dichiarato una seconda volta.
    /// </summary>
    [Fact]
    public void ChiaveOspite_NonDipendeDallOrdineDiNomeECognome()
    {
        Assert.Equal(
            PayTouristDtoBuilder.ChiaveOspite("Ludovico Einaudi", Nascita, Arrivo),
            PayTouristDtoBuilder.ChiaveOspite("Einaudi Ludovico", Nascita, Arrivo));

        Assert.Equal(
            PayTouristDtoBuilder.ChiaveOspite("Ludovico Einaudi", Nascita, Arrivo),
            PayTouristDtoBuilder.ChiaveOspite("Ludovico", "Einaudi", Nascita, Arrivo));
    }

    /// <summary>Maiuscole, accenti e spazi doppi sono differenze di scrittura, non di persona.</summary>
    [Fact]
    public void ChiaveOspite_IgnoraMaiuscoleAccentiESpazi()
    {
        Assert.Equal(
            PayTouristDtoBuilder.ChiaveOspite("Niccolò  DE ANGELIS", Nascita, Arrivo),
            PayTouristDtoBuilder.ChiaveOspite("niccolo de angelis", Nascita, Arrivo));
    }

    /// <summary>Stesso nome ma nato in un altro giorno, o arrivato in un altro giorno: sono due soggiorni diversi, entrambi da dichiarare.</summary>
    [Fact]
    public void ChiaveOspite_DistingueNascitaEArrivoDiversi()
    {
        var chiave = PayTouristDtoBuilder.ChiaveOspite("Ludovico Einaudi", Nascita, Arrivo);

        Assert.NotEqual(chiave, PayTouristDtoBuilder.ChiaveOspite("Ludovico Einaudi", Nascita.AddDays(1), Arrivo));
        Assert.NotEqual(chiave, PayTouristDtoBuilder.ChiaveOspite("Ludovico Einaudi", Nascita, Arrivo.AddDays(1)));
    }

    /// <summary>
    /// Senza data di nascita resterebbero solo nome e cognome: due omonimi arrivati lo stesso giorno
    /// verrebbero presi per la stessa persona e una delle due dichiarazioni non partirebbe. Meglio
    /// nessuna chiave — al più si produce un doppione, che è l'errore meno grave dei due.
    /// </summary>
    [Fact]
    public void ChiaveOspite_SenzaDatiSufficienti_NonProduceChiave()
    {
        Assert.Null(PayTouristDtoBuilder.ChiaveOspite("Ludovico Einaudi", null, Arrivo));
        Assert.Null(PayTouristDtoBuilder.ChiaveOspite("Ludovico Einaudi", Nascita, null));
        Assert.Null(PayTouristDtoBuilder.ChiaveOspite("   ", Nascita, Arrivo));
    }
}
