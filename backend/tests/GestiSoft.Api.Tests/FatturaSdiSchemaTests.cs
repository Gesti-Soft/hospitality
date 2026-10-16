using System.Xml.Linq;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Fatturazione;

namespace GestiSoft.Api.Tests;

/// <summary>
/// Il file che parte verso lo SdI deve passare lo schema ufficiale FPR12 (v1.2.3): ogni caso qui
/// sotto genera l'XML e lo valida davvero contro l'XSD dell'Agenzia, oltre a controllare i campi.
/// </summary>
public class FatturaSdiSchemaTests
{
    private static readonly IntestazioneTrasmissione Canale = new("IT", "ZMMGTN97E08G273L", "01000");

    private static DatiAziendali Azienda() => new()
    {
        Iso2 = "IT",
        PIva = "01234567897",
        CodiceFiscale = "01234567897",
        Denominazione = "Hotel Belvedere S.r.l.",
        RegimeFiscale = RegimeFiscale.RF01_Ordinario,
        Indirizzo = "Via Roma",
        NCivico = "1",
        Cap = "91014",
        Comune = "CASTELLAMMARE DEL GOLFO",
        Provincia = "TP",
    };

    private static DatiCliente ClienteItaliano() => new()
    {
        Iso2 = "IT",
        CodiceFiscale = "RSSMRA80A01H501U",
        Nome = "Mario",
        Cognome = "Rossi",
        Indirizzo = "Via Milano",
        NCivico = "10",
        Cap = "00100",
        LuogoResidenza = "ROMA",
        Provincia = "RM",
    };

    private static DatiCliente ClienteTedescoPrivato() => new()
    {
        Iso2 = "DE",
        Nome = "Jürgen",
        Cognome = "Müller",
        Indirizzo = "Hauptstraße 5, 10115",
        LuogoResidenza = "Berlin",
    };

    private static DatiFattura Fattura(string descrizione = "Soggiorno dal 01/09/2026 al 03/09/2026 (2 notti) — Camera 12 — prenotazione #45")
    {
        var fattura = new DatiFattura
        {
            Progressivo = 7,
            NumeroDocumento = 7,
            DataDocumento = new DateTime(2026, 9, 17),
            Anno = 2026,
            Divisa = "EUR",
            TipoDocumento = TipoDocumentoFattura.TD01_Fattura,
            RegimeFiscale = RegimeFiscale.RF01_Ordinario,
            Righe =
            [
                new RigaFattura { Numero = 1, Descrizione = descrizione, Quantita = 1, PrezzoUnitario = 200, PrezzoTotale = 200, AliquotaIva = AliquotaIva.Iva10 },
            ],
        };
        fattura.PrezzoTotale = CalcoloFattura.Imponibile(fattura.Righe);
        fattura.ImportoTotale = CalcoloFattura.Totale(fattura.Righe, 0);
        return fattura;
    }

    private static XDocument Leggi(byte[] xml)
    {
        using var stream = new MemoryStream(xml);
        return XDocument.Load(stream);
    }

    private static void AssertSchemaValido(byte[] xml)
    {
        var errori = new FatturaDocumentGenerator().ValidaSchema(xml);
        Assert.True(errori.Count == 0, string.Join(Environment.NewLine, errori));
    }

    [Fact]
    public void FatturaItaliana_PassaLoSchemaUfficiale()
    {
        var xml = new FatturaDocumentGenerator().GeneraXmlSdi(Fattura(), ClienteItaliano(), Azienda(), Canale);

        AssertSchemaValido(xml);
    }

    /// <summary>
    /// Sul canale il trasmittente è il titolare del canale, con il codice fiscale: per una ditta
    /// individuale la partita IVA al suo posto fa scartare il file (00300).
    /// </summary>
    [Fact]
    public void Trasmittente_EIlCodiceFiscaleDelCanale_ConIlProgressivoDelFile()
    {
        var xml = Leggi(new FatturaDocumentGenerator().GeneraXmlSdi(Fattura(), ClienteItaliano(), Azienda(), Canale));

        var trasmissione = xml.Descendants("DatiTrasmissione").Single();
        Assert.Equal("IT", trasmissione.Element("IdTrasmittente")!.Element("IdPaese")!.Value);
        Assert.Equal("ZMMGTN97E08G273L", trasmissione.Element("IdTrasmittente")!.Element("IdCodice")!.Value);
        Assert.Equal("01000", trasmissione.Element("ProgressivoInvio")!.Value);
        // La partita IVA del cedente resta la sua: il trasmittente non è chi emette.
        Assert.Equal("01234567897", xml.Descendants("CedentePrestatore").Descendants("IdCodice").Single().Value);
    }

    /// <summary>
    /// Le descrizioni del soggiorno usano il trattino lungo, che il tracciato non ammette: con lui il
    /// file non passa lo schema. Si traslittera, non si toglie.
    /// </summary>
    [Fact]
    public void TrattinoLungoEdEuro_VengonoTraslitterati()
    {
        var xml = new FatturaDocumentGenerator().GeneraXmlSdi(Fattura("Soggiorno — colazione “inclusa” 10 €"), ClienteItaliano(), Azienda(), Canale);

        AssertSchemaValido(xml);
        Assert.Equal("Soggiorno - colazione \"inclusa\" 10 EUR", Leggi(xml).Descendants("Descrizione").First().Value);
    }

    [Fact]
    public void ImportoTotaleDocumento_ComprendeIvaEImpostaDiSoggiorno()
    {
        var fattura = Fattura();
        fattura.ImpostaSoggiorno = 6;
        fattura.ImportoTotale = CalcoloFattura.Totale(fattura.Righe, 6);

        var xml = new FatturaDocumentGenerator().GeneraXmlSdi(fattura, ClienteItaliano(), Azienda(), Canale);

        AssertSchemaValido(xml);
        Assert.Equal("226.00", Leggi(xml).Descendants("ImportoTotaleDocumento").Single().Value);
    }

    /// <summary>
    /// Un ospite straniero senza partita IVA né codice fiscale italiano: identificativo nel paese
    /// vero, convenzionale, e nessun CodiceFiscale (ammette solo il formato italiano). Codice
    /// destinatario XXXXXXX, CAP 00000, niente provincia.
    /// </summary>
    [Fact]
    public void PrivatoStraniero_SenzaCodici_SiFatturaLoStesso()
    {
        var generatore = new FatturaDocumentGenerator();
        var cliente = ClienteTedescoPrivato();

        Assert.Empty(generatore.ValidaPerSdi(Fattura(), cliente, Azienda()));

        var xml = generatore.GeneraXmlSdi(Fattura(), cliente, Azienda(), Canale);
        AssertSchemaValido(xml);

        var documento = Leggi(xml);
        var cessionario = documento.Descendants("CessionarioCommittente").Single();
        Assert.Equal("DE", cessionario.Descendants("IdPaese").Single().Value);
        Assert.Equal("99999999999", cessionario.Descendants("IdCodice").Single().Value);
        Assert.Empty(cessionario.Descendants("CodiceFiscale"));
        Assert.Equal("00000", cessionario.Descendants("CAP").Single().Value);
        Assert.Empty(cessionario.Descendants("Provincia"));
        Assert.Equal("XXXXXXX", documento.Descendants("CodiceDestinatario").Single().Value);
        Assert.Equal("Jürgen", cessionario.Descendants("Nome").Single().Value);
    }

    [Fact]
    public void SenzaCodiceDestinatario_ConPec_LaPecVaNelFile()
    {
        var cliente = ClienteItaliano();
        cliente.Pec = "mario.rossi@pec.it";

        var xml = new FatturaDocumentGenerator().GeneraXmlSdi(Fattura(), cliente, Azienda(), Canale);

        AssertSchemaValido(xml);
        var documento = Leggi(xml);
        Assert.Equal("0000000", documento.Descendants("CodiceDestinatario").Single().Value);
        Assert.Equal("mario.rossi@pec.it", documento.Descendants("PECDestinatario").Single().Value);
    }

    /// <summary>Con un codice destinatario vero lo SdI recapita lì: la PEC non serve e non va scritta.</summary>
    [Fact]
    public void ConCodiceDestinatario_LaPecNonVaNelFile()
    {
        var cliente = ClienteItaliano();
        cliente.CodiceDestinatario = "abc1234";
        cliente.Pec = "mario.rossi@pec.it";

        var documento = Leggi(new FatturaDocumentGenerator().GeneraXmlSdi(Fattura(), cliente, Azienda(), Canale));

        Assert.Equal("ABC1234", documento.Descendants("CodiceDestinatario").Single().Value);
        Assert.Empty(documento.Descendants("PECDestinatario"));
    }

    [Theory]
    [InlineData("ABC123", "sei caratteri")]
    [InlineData("ABC-123", "sette caratteri")]
    public void CodiceDestinatarioSbagliato_VieneSegnalato(string codice, string attesa)
    {
        var cliente = ClienteItaliano();
        cliente.CodiceDestinatario = codice;

        var motivi = new FatturaDocumentGenerator().ValidaPerSdi(Fattura(), cliente, Azienda());

        Assert.Contains(motivi, m => m.Contains(attesa));
    }

    [Fact]
    public void NotaDiCredito_DiceQualeFatturaStorna()
    {
        var originale = Fattura();
        var nota = Fattura("Storno parziale: una notte in meno");
        nota.TipoDocumento = TipoDocumentoFattura.TD04_NotaDiCredito;
        nota.NumeroDocumento = 9;
        nota.FatturaStornataId = originale.Id;
        nota.ModalitaPagamento = ModalitaPagamento.Contanti;

        var generatore = new FatturaDocumentGenerator();
        Assert.Empty(generatore.ValidaPerSdi(nota, ClienteItaliano(), Azienda(), originale));

        var xml = generatore.GeneraXmlSdi(nota, ClienteItaliano(), Azienda(), Canale, originale);
        AssertSchemaValido(xml);

        var documento = Leggi(xml);
        Assert.Equal("TD04", documento.Descendants("TipoDocumento").Single().Value);
        var collegata = documento.Descendants("DatiFattureCollegate").Single();
        Assert.Equal("7", collegata.Element("IdDocumento")!.Value);
        Assert.Equal("2026-09-17", collegata.Element("Data")!.Value);
        // Una nota di credito non dichiara un pagamento.
        Assert.Empty(documento.Descendants("DatiPagamento"));
    }

    [Fact]
    public void NotaDiCredito_SenzaFatturaStornata_NonSiGenera()
    {
        var nota = Fattura();
        nota.TipoDocumento = TipoDocumentoFattura.TD04_NotaDiCredito;
        nota.FatturaStornataId = Guid.NewGuid();

        var motivi = new FatturaDocumentGenerator().ValidaPerSdi(nota, ClienteItaliano(), Azienda(), stornata: null);

        Assert.Contains(motivi, m => m.Contains("storna"));
    }

    [Theory]
    [InlineData(ModalitaPagamento.Contanti, "MP01")]
    [InlineData(ModalitaPagamento.Bonifico, "MP05")]
    [InlineData(ModalitaPagamento.CartaDiPagamento, "MP08")]
    public void ModalitaDiPagamento_FiniscePerIntero_InDatiPagamento(ModalitaPagamento modalita, string codice)
    {
        var fattura = Fattura();
        fattura.ModalitaPagamento = modalita;

        var xml = new FatturaDocumentGenerator().GeneraXmlSdi(fattura, ClienteItaliano(), Azienda(), Canale);

        AssertSchemaValido(xml);
        var dettaglio = Leggi(xml).Descendants("DettaglioPagamento").Single();
        Assert.Equal(codice, dettaglio.Element("ModalitaPagamento")!.Value);
        Assert.Equal("220.00", dettaglio.Element("ImportoPagamento")!.Value);
    }

    [Fact]
    public void ForfettarioConBollo_PassaLoSchema()
    {
        var azienda = Azienda();
        azienda.RegimeFiscale = RegimeFiscale.RF19_Forfettario;
        var fattura = Fattura();
        fattura.RegimeFiscale = RegimeFiscale.RF19_Forfettario;
        fattura.Righe[0].AliquotaIva = AliquotaIva.Iva0;
        fattura.Righe[0].Natura = NaturaIva.N2_2_NonSoggetteAltriCasi;
        fattura.ImportoTotale = CalcoloFattura.Totale(fattura.Righe, 0);
        fattura.ImportoBollo = CalcoloFattura.Bollo(fattura.TipoEmissione, fattura.Righe, 0);

        var xml = new FatturaDocumentGenerator().GeneraXmlSdi(fattura, ClienteItaliano(), azienda, Canale);

        AssertSchemaValido(xml);
        Assert.Equal("200.00", Leggi(xml).Descendants("ImportoTotaleDocumento").Single().Value);
        Assert.Equal("2.00", Leggi(xml).Descendants("ImportoBollo").Single().Value);
    }

    /// <summary>Lo schema deve accorgersi davvero di un file sbagliato, non solo dire sempre di sì.</summary>
    [Fact]
    public void FileSenzaCedente_NonPassaLoSchema()
    {
        var documento = Leggi(new FatturaDocumentGenerator().GeneraXmlSdi(Fattura(), ClienteItaliano(), Azienda(), Canale));
        documento.Descendants("CedentePrestatore").Single().Remove();
        using var stream = new MemoryStream();
        documento.Save(stream);

        Assert.NotEmpty(new FatturaDocumentGenerator().ValidaSchema(stream.ToArray()));
    }

    [Fact]
    public void PartitaIvaDellaStruttura_EObbligatoria()
    {
        var azienda = Azienda();
        azienda.PIva = null;

        var motivi = new FatturaDocumentGenerator().ValidaPerSdi(Fattura(), ClienteItaliano(), azienda);

        Assert.Contains(motivi, m => m.Contains("partita IVA della struttura"));
    }

    /// <summary>Scritti a mano: prefisso IT, spazi, minuscole e omocodia sono codici validi.</summary>
    [Theory]
    [InlineData("IT 012 345 678 97", "rssmra80a01h501u")]
    [InlineData("01234567897", "RSSMRA80A01H50MM")]
    public void PartitaIvaECodiceFiscaleValidi_Passano(string pIvaStruttura, string cfCliente)
    {
        var azienda = Azienda();
        azienda.PIva = pIvaStruttura;
        var cliente = ClienteItaliano();
        cliente.CodiceFiscale = cfCliente;

        Assert.Empty(new FatturaDocumentGenerator().ValidaPerSdi(Fattura(), cliente, azienda));
    }

    /// <summary>Una cifra sbagliata o due invertite si fermano prima dell'invio, non come scarto 00305/00306.</summary>
    [Fact]
    public void PartitaIvaECodiceFiscaleSbagliati_Bloccano()
    {
        var azienda = Azienda();
        azienda.PIva = "01234567879";
        azienda.CodiceFiscale = "01234567890";
        var cliente = ClienteItaliano();
        cliente.CodiceFiscale = "RSSMRA80A01H501X";
        cliente.PIva = "1234567";

        var motivi = new FatturaDocumentGenerator().ValidaPerSdi(Fattura(), cliente, azienda);

        Assert.Contains("la partita IVA della struttura non è valida: controlla le cifre", motivi);
        Assert.Contains("il codice fiscale della struttura non è valido: controlla i caratteri", motivi);
        Assert.Contains("la partita IVA del cliente non è valida: controlla le cifre", motivi);
        Assert.Contains("il codice fiscale del cliente non è valido: controlla i caratteri", motivi);
    }

    /// <summary>Per l'estero non c'è un formato italiano da controllare.</summary>
    [Fact]
    public void ClienteEstero_NonSiControllaIlFormato()
    {
        var cliente = ClienteTedescoPrivato();
        cliente.PIva = "DE123";

        Assert.Empty(new FatturaDocumentGenerator().ValidaPerSdi(Fattura(), cliente, Azienda()));
    }
}
