using System.Xml.Linq;
using GestiSoft.Infrastructure.Wubook;

namespace GestiSoft.Api.Tests;

/// <summary>
/// Lettura di una prenotazione dall'XML-RPC dell'OTA con i campi documentati (tdocs.wubook.net,
/// "Fetching reservations"): due camere dello stesso tipo, `booked_rooms` con i prezzi per notte e
/// `rooms_occupancies`. Nessuna risposta reale dell'OTA è mai stata vista: la forma viene dalla
/// documentazione.
/// </summary>
public class LetturaPrenotazioneOtaTests
{
    private static XElement Membro(string nome, XElement valore) => new("member", new XElement("name", nome), new XElement("value", valore));

    private static XElement Stringa(string v) => new("string", v);

    private static XElement Intero(int v) => new("int", v);

    private static XElement Numero(decimal v) => new("double", v.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static XElement Array(params XElement[] valori) => new("array", new XElement("data", valori.Select(v => new XElement("value", v))));

    private static XElement Struct(params XElement[] membri) => new("struct", membri);

    private static XElement Giorno(string giorno, decimal prezzo) => Struct(Membro("day", Stringa(giorno)), Membro("price", Numero(prezzo)), Membro("rate_id", Intero(0)));

    [Fact]
    public void DueCamereDelloStessoTipo_LetteEntrambe_ConPrezziEOspiti()
    {
        var prenotazione = Struct(
            Membro("reservation_code", Intero(1234)),
            Membro("rooms", Stringa("45052,45052")),
            Membro("date_arrival", Stringa("12/10/2026")),
            Membro("date_departure", Stringa("14/10/2026")),
            Membro("amount", Numero(420m)),
            Membro("men", Intero(3)),
            Membro("children", Intero(1)),
            Membro("booked_rooms", Array(
                Struct(Membro("room_id", Intero(45052)), Membro("roomdays", Array(Giorno("12/10/2026", 100m), Giorno("13/10/2026", 100m)))),
                Struct(Membro("room_id", Intero(45052)), Membro("roomdays", Array(Giorno("12/10/2026", 110m), Giorno("13/10/2026", 110m)))))),
            Membro("rooms_occupancies", Array(
                Struct(Membro("id", Intero(45052)), Membro("occupancy", Intero(2))),
                Struct(Membro("id", Intero(45052)), Membro("occupancy", Intero(2))))));

        var letta = WubookXmlRpcClient.LeggiPrenotazione(prenotazione);

        Assert.NotNull(letta.Camere);
        Assert.Equal(2, letta.Camere.Count);
        Assert.All(letta.Camere, c => Assert.Equal(45052, c.IdCameraWubook));
        Assert.Equal([200m, 220m], letta.Camere.Select(c => c.PrezzoNotti));
        Assert.Equal([2, 2], letta.Camere.Select(c => c.Occupazione));
    }

    [Fact]
    public void SenzaBookedRooms_DalCampoRooms()
    {
        var prenotazione = Struct(
            Membro("reservation_code", Intero(99)),
            Membro("rooms", Stringa("45052, 45053")),
            Membro("amount", Numero(100m)));

        var letta = WubookXmlRpcClient.LeggiPrenotazione(prenotazione);

        Assert.Equal([45052, 45053], letta.Camere!.Select(c => c.IdCameraWubook));
        Assert.All(letta.Camere!, c => Assert.Null(c.Occupazione));
    }
}
