using System.Text;
using System.Xml.Linq;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Fatturazione;

namespace GestiSoft.Api.Tests;

/// <summary>
/// Il file per lo SDI si scarta giorni dopo l'invio, quando di quella fattura non si ricorda più
/// niente: gli errori di tracciato vanno fermati qui, non scoperti da una ricevuta di scarto.
/// I valori usati sono quelli reali di una struttura italiana, compreso il campo "Nazione"
/// compilato come denominazione ("ITALIA"), che è come lo propone il form.
/// </summary>
public class FatturaSdiTests
{
    /// <summary>La licenza QuestPDF la imposta Program.cs all'avvio dell'Api; qui il generatore è usato da solo, quindi va dichiarata anche nei test.</summary>
    static FatturaSdiTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static DatiAziendali Azienda() => new()
    {
        Iso2 = "IT",
        Nazione = "ITALIA",
        PIva = "01234567890",
        Denominazione = "Struttura di prova",
        RegimeFiscale = RegimeFiscale.RF01_Ordinario,
        Indirizzo = "Via Roma",
        NCivico = "1",
        Cap = "91014",
        Comune = "CASTELLAMMARE DEL GOLFO",
        Provincia = "TP",
    };

    private static DatiCliente Cliente() => new()
    {
        Iso2 = "IT",
        CodiceFiscale = "RSSMRA80A01H501U",
        Nome = "Mario",
        Cognome = "Rossi",
        Indirizzo = "Via Milano",
        Cap = "00100",
        LuogoResidenza = "ROMA",
        Provincia = "RM",
    };

    private static DatiFattura Fattura(string? descrizione = "Soggiorno dal 01/09 al 03/09") => new()
    {
        Progressivo = 1,
        NumeroDocumento = 1,
        DataDocumento = new DateTime(2026, 9, 17),
        Divisa = "EUR",
        Righe = [Riga(1, descrizione ?? "", 2, 100, AliquotaIva.Iva10)],
        PrezzoTotale = 200,
        ImportoTotale = 220,
        TipoDocumento = TipoDocumentoFattura.TD01_Fattura,
        RegimeFiscale = RegimeFiscale.RF01_Ordinario,
    };

    private static RigaFattura Riga(int numero, string descrizione, decimal quantita, decimal prezzo, AliquotaIva? aliquota, NaturaIva? natura = null) => new()
    {
        Numero = numero,
        Descrizione = descrizione,
        Quantita = quantita,
        PrezzoUnitario = prezzo,
        PrezzoTotale = quantita * prezzo,
        AliquotaIva = aliquota,
        Natura = natura,
    };

    /// <summary>
    /// Il campo "Nazione" dei dati aziendali è una denominazione, non un codice: finiva nella sede
    /// dell'emittente e bloccava ogni generazione. Con l'ISO2 valorizzato non ci deve essere nulla
    /// da segnalare.
    /// </summary>
    [Fact]
    public void StrutturaItalianaConNazioneScrittaPerEsteso_NonBloccaLaGenerazione()
    {
        var motivi = new FatturaDocumentGenerator().ValidaPerSdi(Fattura(), Cliente(), Azienda());

        Assert.Empty(motivi);
    }

    [Fact]
    public void NazioneDellaSede_EIlCodiceIso2_NonLaDenominazione()
    {
        // Letto dai byte e non dalla stringa: il file esce con il BOM, che XDocument.Parse rifiuta.
        using var stream = new MemoryStream(new FatturaDocumentGenerator().GeneraXmlSdi(Fattura(), Cliente(), Azienda()));
        var xml = XDocument.Load(stream);

        var nazioneEmittente = xml.Descendants("CedentePrestatore").Single().Descendants("Nazione").Single().Value;

        Assert.Equal("IT", nazioneEmittente);
    }

    /// <summary>
    /// Con "ITALIA" al posto di "IT" la struttura risultava estera, e per l'estero CAP e Provincia
    /// non si controllano: mancavano in silenzio su una fattura italiana.
    /// </summary>
    [Fact]
    public void StrutturaItaliana_SenzaCap_VieneSegnalata()
    {
        var azienda = Azienda();
        azienda.Cap = null;

        var motivi = new FatturaDocumentGenerator().ValidaPerSdi(Fattura(), Cliente(), azienda);

        Assert.Contains(motivi, m => m.Contains("CAP", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// L'imposta di soggiorno e una somma anticipata verso il Comune, esclusa dalla base imponibile
    /// ex art. 15: dentro il prezzo del soggiorno pagherebbe un'IVA che non deve, quindi deve stare
    /// su una riga sua con natura N1 e un riepilogo separato.
    /// </summary>
    [Fact]
    public void ImpostaDiSoggiorno_EUnaRigaASe_ConNaturaN1()
    {
        var fattura = Fattura();
        fattura.ImpostaSoggiorno = 12.00m;

        using var stream = new MemoryStream(new FatturaDocumentGenerator().GeneraXmlSdi(fattura, Cliente(), Azienda()));
        var xml = XDocument.Load(stream);

        var righe = xml.Descendants("DettaglioLinee").ToList();
        Assert.Equal(2, righe.Count);

        var rigaTassa = righe[1];
        Assert.Equal("N1", rigaTassa.Element("Natura")!.Value);
        Assert.Equal("12.00", rigaTassa.Element("PrezzoTotale")!.Value);
        Assert.Contains("art. 15", rigaTassa.Element("Descrizione")!.Value);

        // Il riepilogo dell'imponibile con IVA resta separato da quello della somma esclusa.
        var riepiloghi = xml.Descendants("DatiRiepilogo").ToList();
        Assert.Equal(2, riepiloghi.Count);
        Assert.Equal("200.00", riepiloghi[0].Element("ImponibileImporto")!.Value);
        Assert.Equal("12.00", riepiloghi[1].Element("ImponibileImporto")!.Value);
        Assert.Equal("N1", riepiloghi[1].Element("Natura")!.Value);
    }

    /// <summary>
    /// Il totale della fattura comprende l'imposta di soggiorno (FatturazioneService la somma):
    /// l'IVA del riepilogo va calcolata senza, altrimenti 200 € al 10% con 12 € di imposta davano
    /// 32 € di IVA invece di 20, e lo SdI scarta una fattura con imposta incoerente con l'imponibile.
    /// </summary>
    [Fact]
    public void ImpostaDiSoggiorno_NonEntraNellIva()
    {
        var fattura = Fattura();
        fattura.ImpostaSoggiorno = 12.00m;
        fattura.ImportoTotale = 232.00m;

        using var stream = new MemoryStream(new FatturaDocumentGenerator().GeneraXmlSdi(fattura, Cliente(), Azienda()));
        var xml = XDocument.Load(stream);

        var riepilogoIva = xml.Descendants("DatiRiepilogo").First();
        Assert.Equal("200.00", riepilogoIva.Element("ImponibileImporto")!.Value);
        Assert.Equal("20.00", riepilogoIva.Element("Imposta")!.Value);
    }

    /// <summary>
    /// Alloggio al 10% e SPA al 22% sulla stessa fattura: una riga ciascuno e un riepilogo per
    /// aliquota, con l'IVA calcolata sul totale di ogni aliquota. L'aliquota ha due decimali: "10"
    /// senza decimali non rispetta il tracciato.
    /// </summary>
    [Fact]
    public void DueAliquote_DueRighe_DueRiepiloghi()
    {
        var fattura = Fattura();
        fattura.Righe.Add(Riga(2, "SPA del 02/09 (2 persone)", 2, 45, AliquotaIva.Iva22));

        using var stream = new MemoryStream(new FatturaDocumentGenerator().GeneraXmlSdi(fattura, Cliente(), Azienda()));
        var xml = XDocument.Load(stream);

        var righe = xml.Descendants("DettaglioLinee").ToList();
        Assert.Equal(["1", "2"], righe.Select(r => r.Element("NumeroLinea")!.Value));
        Assert.Equal(["10.00", "22.00"], righe.Select(r => r.Element("AliquotaIVA")!.Value));

        var riepiloghi = xml.Descendants("DatiRiepilogo").ToList();
        Assert.Equal(2, riepiloghi.Count);
        Assert.Equal(("22.00", "90.00", "19.80"), (riepiloghi[0].Element("AliquotaIVA")!.Value, riepiloghi[0].Element("ImponibileImporto")!.Value, riepiloghi[0].Element("Imposta")!.Value));
        Assert.Equal(("10.00", "200.00", "20.00"), (riepiloghi[1].Element("AliquotaIVA")!.Value, riepiloghi[1].Element("ImponibileImporto")!.Value, riepiloghi[1].Element("Imposta")!.Value));
    }

    /// <summary>Riga senza IVA (forfettario): AliquotaIVA è obbligatoria e vale 0.00, accanto alla natura.</summary>
    [Fact]
    public void RigaConNatura_HaAliquotaZero()
    {
        var fattura = Fattura();
        fattura.Righe = [Riga(1, "Soggiorno", 1, 200, null, NaturaIva.N2_2_NonSoggetteAltriCasi)];

        using var stream = new MemoryStream(new FatturaDocumentGenerator().GeneraXmlSdi(fattura, Cliente(), Azienda()));
        var xml = XDocument.Load(stream);

        var riga = xml.Descendants("DettaglioLinee").Single();
        Assert.Equal("0.00", riga.Element("AliquotaIVA")!.Value);
        Assert.Equal("N2.2", riga.Element("Natura")!.Value);
    }

    [Fact]
    public void FatturaConPiuRigheEAliquote_ProduceUnPdfValido()
    {
        var fattura = Fattura();
        fattura.Righe.Add(Riga(2, "SPA del 02/09", 2, 45, AliquotaIva.Iva22));
        fattura.ImpostaSoggiorno = 12m;

        var pdf = new FatturaDocumentGenerator().GeneraPdf(fattura, Cliente(), Azienda(), "Hotel di prova");

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void SenzaImpostaDiSoggiorno_LaFatturaHaUnaRigaSola()
    {
        using var stream = new MemoryStream(new FatturaDocumentGenerator().GeneraXmlSdi(Fattura(), Cliente(), Azienda()));
        var xml = XDocument.Load(stream);

        Assert.Single(xml.Descendants("DettaglioLinee"));
        Assert.Single(xml.Descendants("DatiRiepilogo"));
        Assert.Empty(xml.Descendants("DatiBollo"));
    }

    /// <summary>Il bollo va dichiarato nel file, non solo stampato sul PDF.</summary>
    [Fact]
    public void BolloDovuto_FinisceNelBloccoDatiBollo()
    {
        var fattura = Fattura();
        fattura.ImportoBollo = 2.00m;

        using var stream = new MemoryStream(new FatturaDocumentGenerator().GeneraXmlSdi(fattura, Cliente(), Azienda()));
        var xml = XDocument.Load(stream);

        var datiBollo = xml.Descendants("DatiBollo").Single();
        Assert.Equal("SI", datiBollo.Element("BolloVirtuale")!.Value);
        Assert.Equal("2.00", datiBollo.Element("ImportoBollo")!.Value);
    }

    /// <summary>
    /// Il PDF è l'unico documento che esiste per una ricevuta di locazione breve, quindi deve
    /// almeno prodursi: qui con imposta di soggiorno e bollo insieme, il caso con più cose da
    /// mettere sullo stesso foglio.
    /// </summary>
    [Fact]
    public void RicevutaDiLocazioneBreve_ProduceUnPdfValido()
    {
        var ricevuta = Fattura();
        ricevuta.TipoEmissione = TipoEmissioneDocumento.Ricevuta;
        ricevuta.TipoDocumento = null;
        ricevuta.RegimeFiscale = null;
        ricevuta.Righe[0].AliquotaIva = null;
        ricevuta.ImpostaSoggiorno = 18m;
        ricevuta.ImportoBollo = 2.00m;

        var pdf = new FatturaDocumentGenerator().GeneraPdf(ricevuta, Cliente(), Azienda(), "Villa di prova");

        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void FatturaConImpostaDiSoggiorno_ProduceUnPdfValido()
    {
        var fattura = Fattura();
        fattura.ImpostaSoggiorno = 12m;

        var pdf = new FatturaDocumentGenerator().GeneraPdf(fattura, Cliente(), Azienda(), "Hotel di prova");

        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DescrizioneVuota_ImpedisceLaGenerazione(string? descrizione)
    {
        var motivi = new FatturaDocumentGenerator().ValidaPerSdi(Fattura(descrizione), Cliente(), Azienda());

        Assert.Contains(motivi, m => m.Contains("descrizione", StringComparison.OrdinalIgnoreCase));
    }
}
