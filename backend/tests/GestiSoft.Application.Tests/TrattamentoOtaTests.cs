using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Trattamento letto dalle prenotazioni OTA: prima il codice strutturato, poi gli extra comprati,
/// poi il testo del portale solo con frasi esplicite. Nel dubbio non si riconosce nulla.
/// </summary>
public class TrattamentoOtaTests
{
    private static DatiExtraOta Dati(
        string[]? boards = null,
        ExtraOta[]? extra = null,
        (string Chiave, string Valore)[]? ancillary = null,
        string? note = null) =>
        new(
            boards ?? [],
            extra ?? [],
            (ancillary ?? []).Select(a => new KeyValuePair<string, string>(a.Chiave, a.Valore)).ToList(),
            note);

    [Theory]
    [InlineData("bb", TipoTrattamento.Colazione)]
    [InlineData("HB", TipoTrattamento.MezzaPensione)]
    [InlineData("fb", TipoTrattamento.PensioneCompleta)]
    public void CodiceBoard_SiTraduceNelTrattamento(string codice, TipoTrattamento atteso)
    {
        Assert.Equal(new TrattamentoOtaRiconosciuto(true, atteso), TrattamentoOta.Riconosci(Dati(boards: [codice])));
    }

    [Fact]
    public void BoardNb_ESoloPernottamentoEsplicito()
    {
        Assert.Equal(new TrattamentoOtaRiconosciuto(true, null), TrattamentoOta.Riconosci(Dati(boards: ["nb"])));
    }

    [Fact]
    public void AllInclusive_DalCodice()
    {
        Assert.Equal(TipoTrattamento.AllInclusive, TrattamentoOta.Riconosci(Dati(boards: ["ai"])).Trattamento);
    }

    [Fact]
    public void AllInclusive_DalTesto_ComprendeGliAltri()
    {
        var dati = Dati(ancillary: [("info", "All inclusive. Breakfast included.")]);
        Assert.Equal(TipoTrattamento.AllInclusive, TrattamentoOta.Riconosci(dati).Trattamento);
    }

    [Fact]
    public void PiuCamereConTrattamentiDiversi_ValeIlPiuAmpio()
    {
        Assert.Equal(TipoTrattamento.MezzaPensione, TrattamentoOta.Riconosci(Dati(boards: ["bb", "hb"])).Trattamento);
    }

    [Fact]
    public void BoardVinceSulTesto()
    {
        var dati = Dati(boards: ["bb"], ancillary: [("info", "Half board included")]);
        Assert.Equal(TipoTrattamento.Colazione, TrattamentoOta.Riconosci(dati).Trattamento);
    }

    [Fact]
    public void ExtraColazioneComprato_EColazione()
    {
        var dati = Dati(extra: [new ExtraOta("Breakfast", 2, 20m)]);
        Assert.Equal(new TrattamentoOtaRiconosciuto(true, TipoTrattamento.Colazione), TrattamentoOta.Riconosci(dati));
    }

    [Theory]
    [InlineData("Breakfast is included in the room rate.", TipoTrattamento.Colazione)]
    [InlineData("Colazione inclusa", TipoTrattamento.Colazione)]
    [InlineData("Frühstück inklusive", TipoTrattamento.Colazione)]
    [InlineData("Petit-déjeuner inclus", TipoTrattamento.Colazione)]
    [InlineData("Half board", TipoTrattamento.MezzaPensione)]
    [InlineData("Pensione completa", TipoTrattamento.PensioneCompleta)]
    public void FraseEsplicita_SiRiconosce(string testo, TipoTrattamento atteso)
    {
        Assert.Equal(new TrattamentoOtaRiconosciuto(true, atteso), TrattamentoOta.Riconosci(Dati(ancillary: [("meal_plan", testo)])));
    }

    [Theory]
    [InlineData("Breakfast costs EUR 10 per person per night.")]
    [InlineData("Breakfast is not included.")]
    [InlineData("Colazione non inclusa")]
    [InlineData("Half board available on request")]
    [InlineData("Colazione a pagamento")]
    public void FraseConNegazioneOCosto_NonSiRiconosce(string testo)
    {
        Assert.Equal(TrattamentoOtaRiconosciuto.NonIndicato, TrattamentoOta.Riconosci(Dati(ancillary: [("meal_plan", testo)])));
    }

    [Fact]
    public void RoomOnly_ESoloPernottamentoEsplicito()
    {
        Assert.Equal(new TrattamentoOtaRiconosciuto(true, null), TrattamentoOta.Riconosci(Dati(ancillary: [("rate", "Room only")])));
    }

    [Fact]
    public void FrasiInContraddizione_NonSiSceglie()
    {
        var dati = Dati(ancillary: [("a", "Room only"), ("b", "Breakfast included")]);
        Assert.Equal(TrattamentoOtaRiconosciuto.NonIndicato, TrattamentoOta.Riconosci(dati));
    }

    [Fact]
    public void RichiesteOspite_NonContanoComeAcquisto()
    {
        Assert.Equal(TrattamentoOtaRiconosciuto.NonIndicato, TrattamentoOta.Riconosci(Dati(note: "Breakfast included please")));
    }

    [Fact]
    public void NienteDati_NonIndicato()
    {
        Assert.Equal(TrattamentoOtaRiconosciuto.NonIndicato, TrattamentoOta.Riconosci(null));
        Assert.Equal(TrattamentoOtaRiconosciuto.NonIndicato, TrattamentoOta.Riconosci(Dati()));
    }

    [Fact]
    public void Note_SoloRichiesteEOraDiArrivo()
    {
        var note = TrattamentoOta.ComponiNote(Dati(
            boards: ["bb"],
            extra: [new ExtraOta("Parcheggio", 3, 15m)],
            ancillary: [("remarks", "Arrivo tardi"), ("ora_arrivo", "15:00"), ("animale", "si")],
            note: "Culla in camera"));

        Assert.Equal("Richieste dell'ospite: Culla in camera\nOra di arrivo: 15:00\nAnimale: sì", note);
    }

    [Fact]
    public void Note_SenzaAnimale_NessunaRiga()
    {
        var note = TrattamentoOta.ComponiNote(Dati(ancillary: [("ora_arrivo", "15:00"), ("animale", "no")]));

        Assert.Equal("Ora di arrivo: 15:00", note);
    }

    [Fact]
    public void Note_SenzaIlBloccoDiDettagliDelSito()
    {
        var note = TrattamentoOta.ComponiNote(Dati(
            ancillary: [("ora_arrivo", "15:00")],
            note: "Sono Bellissima\n\n--- Dettagli prenotazione (sito web) ---\nOspiti: 2\nAnimale: sì (supplemento 100 €)\nOra di arrivo: 15:00"));

        Assert.Equal("Richieste dell'ospite: Sono Bellissima\nOra di arrivo: 15:00", note);
    }

    [Fact]
    public void Note_SoloIlBloccoDelSito_NessunaRichiesta()
    {
        var note = TrattamentoOta.ComponiNote(Dati(note: "--- Dettagli prenotazione (sito web) ---\nOspiti: 2"));

        Assert.Null(note);
    }

    [Fact]
    public void Note_SenzaDatiDiCarta()
    {
        var note = TrattamentoOta.ComponiNote(Dati(note: "Pagato con 4111 1111 1111 1111"));

        Assert.Equal("Richieste dell'ospite: Pagato con [numero rimosso]", note);
    }

    [Theory]
    [InlineData("si", true)]
    [InlineData("Sì", true)]
    [InlineData("no", false)]
    public void Animale_DalSitoWeb(string valore, bool atteso)
    {
        Assert.Equal(atteso, ServiziSitoWeb.Animale(Dati(ancillary: [("animale", valore)])));
    }

    [Fact]
    public void Animale_NonIndicatoDaiPortali()
    {
        Assert.Null(ServiziSitoWeb.Animale(Dati(ancillary: [("remarks", "Arrivo tardi")])));
    }

    [Fact]
    public void Note_NullSeNonCeNiente()
    {
        Assert.Null(TrattamentoOta.ComponiNote(Dati()));
        Assert.Null(TrattamentoOta.ComponiNote(null));
    }

    [Fact]
    public void Note_TroncateAllaLunghezzaDellaColonna()
    {
        var note = TrattamentoOta.ComponiNote(Dati(note: new string('a', 5000)));
        Assert.Equal(TrattamentoOta.LunghezzaMassimaNote, note!.Length);
    }

    [Fact]
    public void CodiceDelSitoWeb_ColazioneAncheSenzaFraseEsplicita()
    {
        // Il testo "Colazione" da solo non basta, il codice del sito sì.
        var dati = Dati(ancillary: [("trattamento", "Colazione"), ("trattamento_codice", "bb")]);

        Assert.Equal(new TrattamentoOtaRiconosciuto(true, TipoTrattamento.Colazione), TrattamentoOta.Riconosci(dati));
    }

    [Fact]
    public void CodiceDelSitoWeb_SoloPernottamentoEsplicito()
    {
        Assert.Equal(new TrattamentoOtaRiconosciuto(true, null), TrattamentoOta.Riconosci(Dati(ancillary: [("trattamento_codice", "nb")])));
    }

    [Fact]
    public void Boards_VinconoSulCodiceDelSito()
    {
        var dati = Dati(boards: ["fb"], ancillary: [("trattamento_codice", "bb")]);

        Assert.Equal(new TrattamentoOtaRiconosciuto(true, TipoTrattamento.PensioneCompleta), TrattamentoOta.Riconosci(dati));
    }

    [Fact]
    public void CodiceDelSitoSconosciuto_SiPassaAlTesto()
    {
        var dati = Dati(ancillary: [("trattamento_codice", "xx"), ("trattamento", "Mezza pensione")]);

        Assert.Equal(new TrattamentoOtaRiconosciuto(true, TipoTrattamento.MezzaPensione), TrattamentoOta.Riconosci(dati));
    }
}
