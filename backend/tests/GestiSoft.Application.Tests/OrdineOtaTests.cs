using GestiSoft.Application.Wubook;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Un ordine OTA con più camere diventa una prenotazione per camera: importo diviso senza perdere
/// centesimi, ospiti per camera. Prima un ordine con "45052,45052" veniva scartato.
/// </summary>
public class OrdineOtaTests
{
    private static WubookPrenotazione Ordine(decimal importo, int adulti, int bambini, string rooms, params CameraOrdineOta[] camere) => new(
        RCode: 1,
        ChannelReservationCode: "BK-1",
        CameraIdWubookRaw: rooms,
        CheckIn: new DateTime(2026, 10, 12),
        CheckOut: new DateTime(2026, 10, 15),
        Importo: importo,
        Adulti: adulti,
        Bambini: bambini,
        Status: 1,
        IdChannel: 2,
        CustomerName: null,
        CustomerSurname: null,
        CustomerEmail: null,
        CustomerCountry: null,
        CustomerCity: null,
        Camere: camere);

    [Fact]
    public void UnaCamera_ComePrima()
    {
        var camere = OrdineOta.Suddividi(Ordine(300m, 2, 1, "45052", new CameraOrdineOta(45052, 280m, 3)));

        var camera = Assert.Single(camere);
        Assert.Equal((0, 45052, 300m, 3), (camera.Indice, camera.IdCameraWubook, camera.Importo, camera.NumeroOspiti));
    }

    [Fact]
    public void DueCamereDelloStessoTipo_DuePrenotazioni()
    {
        var camere = OrdineOta.Suddividi(Ordine(600m, 4, 0, "45052,45052",
            new CameraOrdineOta(45052, 300m, 2),
            new CameraOrdineOta(45052, 300m, 2)));

        Assert.Equal([0, 1], camere.Select(c => c.Indice));
        Assert.All(camere, c => Assert.Equal(45052, c.IdCameraWubook));
        Assert.Equal([300m, 300m], camere.Select(c => c.Importo));
        Assert.Equal([2, 2], camere.Select(c => c.NumeroOspiti));
    }

    [Fact]
    public void ImportoInProporzioneAiPrezzi_ResiduoSullaPrima()
    {
        // 100 € diviso in 1/3 e 2/3: 33,33 + 66,67 = 100,00.
        var importi = OrdineOta.DividiImporto(100m, [100m, 200m]);

        Assert.Equal([33.33m, 66.67m], importi);
        Assert.Equal(100m, importi.Sum());
    }

    [Fact]
    public void SenzaPrezziPerCamera_ImportoInPartiUguali()
    {
        var importi = OrdineOta.DividiImporto(100m, [0m, 0m, 0m]);

        Assert.Equal(100m, importi.Sum());
        Assert.Equal([33.34m, 33.33m, 33.33m], importi);
    }

    [Fact]
    public void SenzaOccupazioni_OspitiDivisi_AlmenoUnoPerCamera()
    {
        Assert.Equal([3, 2], OrdineOta.DividiOspiti(5, [null, null]));
        Assert.Equal([1, 1], OrdineOta.DividiOspiti(1, [null, null]));
    }

    [Fact]
    public void DalSoloElencoRooms_SeMancanoLeCamere()
    {
        var camere = OrdineOta.Suddividi(Ordine(200m, 2, 0, "45052,45053"));

        Assert.Equal([45052, 45053], camere.Select(c => c.IdCameraWubook));
        Assert.Equal(200m, camere.Sum(c => c.Importo));
    }

    [Fact]
    public void ConCommissione_UnaCamera_TuttaSua()
    {
        var camera = Assert.Single(OrdineOta.Suddividi(Ordine(478.43m, 2, 0, "45052", new CameraOrdineOta(45052, 378.43m, 2)), 111.57m));

        Assert.Equal((478.43m, 111.57m), (camera.Importo, camera.Commissione));
    }

    /// <summary>La commissione si divide con gli stessi pesi dell'importo, senza perdere centesimi.</summary>
    [Fact]
    public void ConCommissione_DueCamere_DivisaInProporzione()
    {
        var camere = OrdineOta.Suddividi(
            Ordine(300m, 4, 0, "45052,45053", new CameraOrdineOta(45052, 100m, 2), new CameraOrdineOta(45053, 200m, 2)), 50.01m);

        Assert.Equal([16.67m, 33.34m], camere.Select(c => c.Commissione!.Value));
        Assert.Equal(50.01m, camere.Sum(c => c.Commissione!.Value));
    }

    [Fact]
    public void SenzaCommissione_Null()
    {
        Assert.All(OrdineOta.Suddividi(Ordine(200m, 2, 0, "45052,45053")), c => Assert.Null(c.Commissione));
    }

    [Fact]
    public void IdNonNumerico_Rifiutato()
    {
        Assert.Throws<InvalidOperationException>(() => OrdineOta.Suddividi(Ordine(100m, 2, 0, "abc")));
    }
}
