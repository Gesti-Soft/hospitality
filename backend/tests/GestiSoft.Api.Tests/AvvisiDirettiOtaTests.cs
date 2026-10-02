using System.Net;
using System.Text;
using System.Xml.Linq;
using GestiSoft.Infrastructure.Wubook;
using Microsoft.Extensions.Configuration;

namespace GestiSoft.Api.Tests;

/// <summary>
/// Ricezione diretta delle prenotazioni: forma delle chiamate all'OTA (tdocs.wubook.net/wired/fetch.html)
/// e blocco dell'attivazione fuori dalla produzione. Nessuna risposta reale dell'OTA è mai stata
/// vista: la forma viene dalla documentazione.
/// </summary>
public class AvvisiDirettiOtaTests
{
    private sealed class ServerFinto(string risposta) : HttpMessageHandler
    {
        public XDocument? Richiesta { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Richiesta = XDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(risposta, Encoding.UTF8, "text/xml") };
        }
    }

    private static string Risposta(string dati) =>
        $"<methodResponse><params><param><value><array><data><value><int>0</int></value>{dati}</data></array></value></param></params></methodResponse>";

    private static List<string> Parametri(XDocument richiesta) =>
        richiesta.Descendants("param").Select(p => p.Element("value")!.Elements().First().Value).ToList();

    [Fact]
    public async Task FetchBookings_PerDataDiCreazione_ELeggeLePrenotazioni()
    {
        var server = new ServerFinto(Risposta(
            "<value><array><data><value><struct>" +
            "<member><name>reservation_code</name><value><int>555</int></value></member>" +
            "<member><name>rooms</name><value><string>45052</string></value></member>" +
            "<member><name>date_arrival</name><value><string>12/10/2026</string></value></member>" +
            "<member><name>date_departure</name><value><string>14/10/2026</string></value></member>" +
            "</struct></value></data></array></value>"));
        var client = new WubookXmlRpcClient(new HttpClient(server));

        var prenotazioni = await client.FetchBookingsCreateAsync("tok", "123", new DateTime(2026, 9, 29), new DateTime(2026, 10, 3), CancellationToken.None);

        Assert.Equal("fetch_bookings", server.Richiesta!.Descendants("methodName").Single().Value);
        // token, lcode, dfrom, dto, oncreated=1, ancillary=1
        Assert.Equal(["tok", "123", "29/09/2026", "03/10/2026", "1", "1"], Parametri(server.Richiesta));
        Assert.Equal(555, Assert.Single(prenotazioni).RCode);
    }

    [Fact]
    public async Task PushActivation_ConIndirizzoEProva()
    {
        var server = new ServerFinto(Risposta(""));
        var client = new WubookXmlRpcClient(new HttpClient(server));

        await client.PushActivationAsync("tok", "123", "https://gestionale.example/api/ota/avviso/abc", prova: true, CancellationToken.None);

        Assert.Equal("push_activation", server.Richiesta!.Descendants("methodName").Single().Value);
        Assert.Equal(["tok", "123", "https://gestionale.example/api/ota/avviso/abc", "1"], Parametri(server.Richiesta));
    }

    [Theory]
    [InlineData("<value><string>https://gestisoft.it/wubook/push</string></value>", "https://gestisoft.it/wubook/push")]
    [InlineData("<value><string></string></value>", null)]
    [InlineData("", null)]
    public async Task PushUrl_LeggeLIndirizzoRegistrato(string dati, string? atteso)
    {
        var client = new WubookXmlRpcClient(new HttpClient(new ServerFinto(Risposta(dati))));

        Assert.Equal(atteso, await client.PushUrlAsync("tok", "123", CancellationToken.None));
    }

    private static IConfiguration Configurazione(string? origine, string? segreto) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:Origin"] = origine, ["OtaAvvisi:Segreto"] = segreto })
            .Build();

    private const string SegretoValido = "abcdefabcdefabcdefabcdef1234";

    [Fact]
    public void Indirizzo_InProduzione()
    {
        var (url, motivo) = OtaAvvisiIndirizzo.Costruisci(Configurazione("https://gestionale.example/", SegretoValido));

        Assert.Equal($"https://gestionale.example/api/ota/avviso/{SegretoValido}", url);
        Assert.Null(motivo);
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://localhost:8081")]
    [InlineData("http://gestionale.example")]
    [InlineData(null)]
    public void Indirizzo_DalloSviluppoNonSiAttiva(string? origine)
    {
        // Lo sviluppo ha le credenziali della produzione: attivare da qui sposterebbe gli avvisi della struttura vera.
        var (url, motivo) = OtaAvvisiIndirizzo.Costruisci(Configurazione(origine, SegretoValido));

        Assert.Null(url);
        Assert.NotNull(motivo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("corto")]
    public void Indirizzo_SenzaSegretoNonSiAttiva(string? segreto)
    {
        var (url, _) = OtaAvvisiIndirizzo.Costruisci(Configurazione("https://gestionale.example", segreto));

        Assert.Null(url);
    }
}
