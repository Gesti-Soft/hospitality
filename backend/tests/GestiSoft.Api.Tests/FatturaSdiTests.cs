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
        Descrizione = descrizione,
        Quantita = 2,
        PrezzoUnitario = 100,
        PrezzoTotale = 200,
        ImportoTotale = 220,
        AliquotaIva = AliquotaIva.Iva10,
        TipoDocumento = TipoDocumentoFattura.TD01_Fattura,
        RegimeFiscale = RegimeFiscale.RF01_Ordinario,
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
