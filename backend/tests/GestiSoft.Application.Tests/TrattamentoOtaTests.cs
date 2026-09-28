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
    public void Note_RiportanoRichiesteTrattamentoExtraEAncillary()
    {
        var note = TrattamentoOta.ComponiNote(Dati(
            boards: ["bb"],
            extra: [new ExtraOta("Parcheggio", 3, 15m)],
            ancillary: [("remarks", "Arrivo tardi")],
            note: "Culla in camera"));

        Assert.Equal(
            "Richieste dell'ospite: Culla in camera\nTrattamento: colazione\nExtra: Parcheggio x3, 15,00 €\nremarks: Arrivo tardi",
            note);
    }

    [Fact]
    public void Note_SenzaDatiDiCarta()
    {
        var note = TrattamentoOta.ComponiNote(Dati(ancillary: [
            ("vcc_number", "4111111111111111"),
            ("card_expiry", "12/28"),
            ("remarks", "Pagato con 4111 1111 1111 1111"),
        ]));

        Assert.Equal("remarks: Pagato con [numero rimosso]", note);
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
}
