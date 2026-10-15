using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Common;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Fatturazione.Sdi;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Notifiche;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Fatturazione;
using GestiSoft.Sdi;

namespace GestiSoft.Api.Tests;

/// <summary>
/// Il canale SdI dal lato di chi riceve: le ricevute sulle fatture spedite (con gli esempi ufficiali
/// dell'Agenzia), la risposta di presa in carico, la risposta MTOM di RiceviFile e le fatture dei
/// fornitori in arrivo.
/// </summary>
public class SdiCanaleTests
{
    private static byte[] Esempio(string tipo) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "MessaggiSdi", $"IT01234567890_11111_{tipo}_001.xml"));

    // ------------------------------------------------------------------ lettura delle ricevute

    [Theory]
    [InlineData("RicevutaConsegna", TipoRicevutaSdi.Consegna)]
    [InlineData("NotificaScarto", TipoRicevutaSdi.Scarto)]
    [InlineData("RicevutaScarto", TipoRicevutaSdi.Scarto)]
    [InlineData("NotificaMancataConsegna", TipoRicevutaSdi.MancataConsegna)]
    [InlineData("RicevutaImpossibilitaRecapito", TipoRicevutaSdi.MancataConsegna)]
    [InlineData("NotificaDecorrenzaTermini", TipoRicevutaSdi.DecorrenzaTermini)]
    [InlineData("AttestazioneTrasmissioneFattura", TipoRicevutaSdi.AttestazioneTrasmissione)]
    [InlineData("QualcosaDiNuovo", TipoRicevutaSdi.Sconosciuta)]
    public void TipoDallaRadice_SenzaPrefisso(string radice, TipoRicevutaSdi atteso) =>
        Assert.Equal(atteso, LettoreXmlSdi.TipoRicevuta(radice));

    [Fact]
    public void RicevutaDiConsegnaUfficiale_LaDescrizioneDelCanaleNonFiniscesullEsito()
    {
        var letta = LettoreXmlSdi.LeggiRicevuta(Esempio("RC"))!;

        Assert.Equal(TipoRicevutaSdi.Consegna, letta.Tipo);
        Assert.Equal("111", letta.IdentificativoSdi);
        Assert.Equal("IT01234567890_11111.xml.p7m", letta.NomeFile);
        // "Amministrazione di prova" sta dentro <Destinatario>: non è l'esito.
        Assert.DoesNotContain("Amministrazione", letta.Testo ?? "");
    }

    [Fact]
    public void ScartoUfficiale_RiportaCodiceEDescrizioneDegliErrori()
    {
        var letta = LettoreXmlSdi.LeggiRicevuta(Esempio("NS"))!;

        Assert.Equal(TipoRicevutaSdi.Scarto, letta.Tipo);
        Assert.Equal("00100", Assert.Single(letta.Errori).Codice);
        Assert.Contains("00100 Certificato di firma scaduto", letta.Testo);
    }

    [Fact]
    public void TestoCodificatoDueVolte_SiRipara()
    {
        Assert.Equal("Il file è stato scartato", LettoreXmlSdi.RiparaDoppiaCodifica("Il file Ã¨ stato scartato"));
        Assert.Equal("Già corretto: è", LettoreXmlSdi.RiparaDoppiaCodifica("Già corretto: è"));
    }

    // ------------------------------------------------------------------ ricevute sulle fatture

    private sealed class Scenario
    {
        public readonly Guid StrutturaId = Guid.NewGuid();
        public readonly SdiFinto Sdi = new();
        public readonly FattureFinte Fatture = new();
        public readonly NotificheFinte Notifiche = new();
        public readonly CanaleFinto Canale = new();
        public readonly DatiFattura Fattura;
        public readonly TrasmissioneFattura Trasmissione;

        public Scenario(bool trasmissioneDiProva = false)
        {
            Fattura = new DatiFattura { StrutturaId = StrutturaId, NumeroDocumento = 5, Anno = 2026, StatoSdi = trasmissioneDiProva ? StatoSdiFattura.DaInviare : StatoSdiFattura.Inviata, IdentificativoSdi = trasmissioneDiProva ? null : "111" };
            Trasmissione = new TrasmissioneFattura
            {
                StrutturaId = StrutturaId, DatiFatturaId = Fattura.Id, NomeFile = "IT01234567890_11111.xml.p7m", IdentificativoSdi = "111",
                Esito = EsitoTrasmissioneSdi.Inviato, Prova = trasmissioneDiProva, ViaCanale = true,
            };
            Fatture.Elenco[Fattura.Id] = Fattura;
            Sdi.Trasmissioni.Add(Trasmissione);
        }

        public SdiRicevuteService Servizio() => new(Sdi, Fatture, new StruttureFinte(), new NotificaService(Notifiche, null!, null!, null!, null!, null!), new LogFinto(), Canale);
    }

    [Fact]
    public async Task RicevutaDiConsegna_LaFatturaDiventaConsegnata_ESiAvvisaLaStruttura()
    {
        var s = new Scenario();

        await s.Servizio().ImportaAsync("IT01234567890_11111_RC_001.xml", "111", Esempio("RC"), prova: false, CancellationToken.None);

        Assert.Equal(StatoSdiFattura.Consegnata, s.Fattura.StatoSdi);
        Assert.Equal(EsitoTrasmissioneSdi.Consegnato, s.Trasmissione.Esito);
        var ricevuta = Assert.Single(s.Sdi.Ricevute);
        Assert.Equal(s.StrutturaId, ricevuta.StrutturaId);
        var notifica = Assert.Single(s.Notifiche.Elenco);
        Assert.Equal(TipoNotifica.EsitoFatturaSdi, notifica.Tipo);
        Assert.Contains("consegnata", notifica.Titolo);
    }

    [Fact]
    public async Task Scarto_LaFatturaDiventaScartata_ConIlMotivo()
    {
        var s = new Scenario();

        await s.Servizio().ImportaAsync("x_NS_001.xml", "111", Esempio("NS"), prova: false, CancellationToken.None);

        Assert.Equal(StatoSdiFattura.Scartata, s.Fattura.StatoSdi);
        Assert.Contains("00100", s.Fattura.NotaSdi);
    }

    /// <summary>Impossibilità di recapito: la fattura è valida, non è un rifiuto.</summary>
    [Fact]
    public async Task MancataConsegna_LaFatturaENonConsegnata()
    {
        var s = new Scenario();

        await s.Servizio().ImportaAsync("x_MC_001.xml", "111", Esempio("MC"), prova: false, CancellationToken.None);

        Assert.Equal(StatoSdiFattura.NonConsegnata, s.Fattura.StatoSdi);
    }

    /// <summary>In collaudo la ricevuta si conserva e aggiorna il file di prova, ma la fattura non si tocca.</summary>
    [Fact]
    public async Task RicevutaDiCollaudo_NonToccaLaFattura()
    {
        var s = new Scenario(trasmissioneDiProva: true);

        await s.Servizio().ImportaAsync("x_RC_001.xml", "111", Esempio("RC"), prova: true, CancellationToken.None);

        Assert.Equal(StatoSdiFattura.DaInviare, s.Fattura.StatoSdi);
        Assert.Equal(EsitoTrasmissioneSdi.Consegnato, s.Trasmissione.Esito);
        Assert.True(Assert.Single(s.Sdi.Ricevute).Prova);
        Assert.Empty(s.Notifiche.Elenco);
    }

    /// <summary>Ambiente di test con gli esiti applicati: la ricevuta di collaudo fa il giro di produzione.</summary>
    [Fact]
    public async Task RicevutaDiCollaudo_ConEsitiApplicati_LaFatturaDiventaConsegnata()
    {
        var s = new Scenario(trasmissioneDiProva: true);
        s.Canale.CollaudoApplicaEsiti = true;

        await s.Servizio().ImportaAsync("x_RC_001.xml", "111", Esempio("RC"), prova: true, CancellationToken.None);

        Assert.Equal(StatoSdiFattura.Consegnata, s.Fattura.StatoSdi);
        Assert.Single(s.Notifiche.Elenco);
    }

    /// <summary>Anche con gli esiti applicati, collaudo e produzione non si mescolano.</summary>
    [Fact]
    public async Task RicevutaDiCollaudo_SuUnFileDiProduzione_NonToccaLaFattura()
    {
        var s = new Scenario();
        s.Canale.CollaudoApplicaEsiti = true;

        await s.Servizio().ImportaAsync("x_RC_001.xml", "111", Esempio("RC"), prova: true, CancellationToken.None);

        Assert.Equal(StatoSdiFattura.Inviata, s.Fattura.StatoSdi);
        Assert.Empty(s.Notifiche.Elenco);
    }

    /// <summary>Decorrenza termini dopo la consegna: si annota e non smentisce nulla.</summary>
    [Fact]
    public async Task DecorrenzaTermini_NonCambiaLoStato()
    {
        var s = new Scenario();
        s.Fattura.StatoSdi = StatoSdiFattura.Consegnata;

        await s.Servizio().ImportaAsync("x_DT_001.xml", "111", Esempio("DT"), prova: false, CancellationToken.None);

        Assert.Equal(StatoSdiFattura.Consegnata, s.Fattura.StatoSdi);
        Assert.Single(s.Sdi.Ricevute);
    }

    [Fact]
    public async Task RicevutaSenzaFattura_SiConservaSenzaStruttura()
    {
        var s = new Scenario();
        s.Sdi.Trasmissioni.Clear();

        await s.Servizio().ImportaAsync("x_RC_001.xml", "111", Esempio("RC"), prova: false, CancellationToken.None);

        Assert.Null(Assert.Single(s.Sdi.Ricevute).StrutturaId);
    }

    [Fact]
    public async Task RicevutaIlleggibile_SiConservaLoStesso_SenzaErrori()
    {
        var s = new Scenario();

        await s.Servizio().ImportaAsync("x.xml", "111", Encoding.UTF8.GetBytes("non è xml"), prova: false, CancellationToken.None);

        Assert.Equal(TipoRicevutaSdi.Sconosciuta, Assert.Single(s.Sdi.Ricevute).Tipo);
        Assert.Equal(StatoSdiFattura.Inviata, s.Fattura.StatoSdi);
    }

    // ------------------------------------------------------------------ SOAP

    /// <summary>
    /// La presa in carico rispecchia la richiesta: radice nel suo namespace, Esito senza namespace e
    /// con xmlns="" esplicito. Con Esito qualificato, lo SdI legge un esito nullo.
    /// </summary>
    [Fact]
    public void RispostaEr01_EsitoSenzaNamespace()
    {
        const string busta = """
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
              <soapenv:Body>
                <ns2:fileSdIConMetadati xmlns:ns2="http://www.fatturapa.gov.it/sdi/ws/ricezione/v1.0/types">
                  <IdentificativoSdI>32667922</IdentificativoSdI>
                  <NomeFile>IT01234567890_abc12.xml</NomeFile>
                  <File>PGE+PC9hPg==</File>
                  <NomeFileMetadati>IT01234567890_abc12_MT_001.xml</NomeFileMetadati>
                  <Metadati>PGE+PC9hPg==</Metadati>
                </ns2:fileSdIConMetadati>
              </soapenv:Body>
            </soapenv:Envelope>
            """;

        var chiamata = ServizioSoap.Leggi(Encoding.UTF8.GetBytes(busta))!;
        var risposta = ServizioSoap.RispostaEr01(chiamata.Operazione);

        Assert.Equal("32667922", chiamata.IdentificativoSdi);
        Assert.Contains("<rispostaRiceviFatture xmlns=\"http://www.fatturapa.gov.it/sdi/ws/ricezione/v1.0/types\"><Esito xmlns=\"\">ER01</Esito>", risposta);
    }

    [Fact]
    public void Base64SuPiuRighe_SiLegge()
    {
        Assert.Equal("ciao mondo", Encoding.UTF8.GetString(ServizioSoap.Base64("Y2lhbyBt\r\nb25kbw==")!));
    }

    /// <summary>La risposta di RiceviFile arriva in MTOM: la busta sta nella prima parte del multipart.</summary>
    [Fact]
    public void RispostaMtom_SiEstraeLaBusta()
    {
        const string corpo =
            "--uuid:abc\r\nContent-Type: application/xop+xml; charset=UTF-8; type=\"text/xml\"\r\n\r\n" +
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><S:Envelope xmlns:S=\"http://schemas.xmlsoap.org/soap/envelope/\"><S:Body>" +
            "<ns2:rispostaSdIRiceviFile xmlns:ns2=\"http://www.fatturapa.gov.it/sdi/ws/trasmissione/v1.0/types\">" +
            "<IdentificativoSdI>4242</IdentificativoSdI><DataOraRicezione>2026-10-07T12:00:00.000+02:00</DataOraRicezione>" +
            "</ns2:rispostaSdIRiceviFile></S:Body></S:Envelope>\r\n--uuid:abc--";

        var risposta = SdiTrasmissioneClient.Interpreta(Encoding.UTF8.GetBytes(corpo), 200);

        Assert.True(risposta.Accettato);
        Assert.Equal("4242", risposta.IdentificativoSdi);
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero), risposta.DataOraRicezione);
    }

    [Fact]
    public void RispostaConErrore_NonEAccettata()
    {
        const string corpo = "<S:Envelope xmlns:S=\"http://schemas.xmlsoap.org/soap/envelope/\"><S:Body><ns2:rispostaSdIRiceviFile xmlns:ns2=\"x\">" +
            "<IdentificativoSdI>0</IdentificativoSdI><DataOraRicezione>2026-10-07T12:00:00Z</DataOraRicezione><Errore>EI02</Errore></ns2:rispostaSdIRiceviFile></S:Body></S:Envelope>";

        var risposta = SdiTrasmissioneClient.Interpreta(Encoding.UTF8.GetBytes(corpo), 200);

        Assert.False(risposta.Accettato);
        Assert.Equal("EI02", risposta.Errore);
    }

    // ------------------------------------------------------------------ fatture dei fornitori

    private static byte[] FatturaFornitore(string cessionarioPiva = "01234567890", int documenti = 1)
    {
        var body = string.Concat(Enumerable.Range(1, documenti).Select(i => $"""
            <FatturaElettronicaBody>
              <DatiGenerali><DatiGeneraliDocumento><TipoDocumento>TD01</TipoDocumento><Divisa>EUR</Divisa><Data>2026-10-01</Data><Numero>A/{i}</Numero><ImportoTotaleDocumento>122.00</ImportoTotaleDocumento></DatiGeneraliDocumento></DatiGenerali>
              <DatiBeniServizi><DatiRiepilogo><AliquotaIVA>22.00</AliquotaIVA><ImponibileImporto>100.00</ImponibileImporto><Imposta>22.00</Imposta></DatiRiepilogo></DatiBeniServizi>
            </FatturaElettronicaBody>
            """));
        return Encoding.UTF8.GetBytes($"""
            <?xml version="1.0" encoding="UTF-8"?>
            <p:FatturaElettronica versione="FPR12" xmlns:p="http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2">
              <FatturaElettronicaHeader>
                <CedentePrestatore><DatiAnagrafici><IdFiscaleIVA><IdPaese>IT</IdPaese><IdCodice>09876543210</IdCodice></IdFiscaleIVA><Anagrafica><Denominazione>Lavanderia Industriale S.r.l.</Denominazione></Anagrafica></DatiAnagrafici></CedentePrestatore>
                <CessionarioCommittente><DatiAnagrafici><IdFiscaleIVA><IdPaese>IT</IdPaese><IdCodice>{cessionarioPiva}</IdCodice></IdFiscaleIVA></DatiAnagrafici></CessionarioCommittente>
              </FatturaElettronicaHeader>
              {body}
            </p:FatturaElettronica>
            """);
    }

    /// <summary>Un .p7m: il contenuto firmato sta in chiaro dentro l'involucro binario.</summary>
    private static byte[] P7m(byte[] xml) => [0x30, 0x80, 0x06, 0x09, .. Encoding.ASCII.GetBytes("junk"), .. xml, 0xA0, 0x82, 0x01, 0x00, .. Encoding.ASCII.GetBytes("firma")];

    /// <summary>Un .p7m vero: CMS SignedData firmato con un certificato di prova, contenuto incluso.</summary>
    private static byte[] P7mFirmato(byte[] xml)
    {
        using var chiave = RSA.Create(2048);
        var richiesta = new CertificateRequest("CN=Firma di prova", chiave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificato = richiesta.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var cms = new SignedCms(new ContentInfo(xml), detached: false);
        cms.ComputeSignature(new CmsSigner(certificato));
        return cms.Encode();
    }

    /// <summary>
    /// Lo stesso .p7m riscritto in BER come fanno molti software di firma: lunghezze indefinite e
    /// contenuto spezzato in blocchi (OCTET STRING costruita), con le intestazioni in mezzo all'XML.
    /// </summary>
    private static byte[] P7mABlocchi(byte[] xml, int blocco)
    {
        var contentInfo = new AsnReader(P7mFirmato(xml), AsnEncodingRules.DER).ReadSequence();
        var oidSignedData = contentInfo.ReadEncodedValue().ToArray();
        var signedData = contentInfo.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0)).ReadSequence();
        var versione = signedData.ReadEncodedValue().ToArray();
        var algoritmi = signedData.ReadEncodedValue().ToArray();
        var encap = signedData.ReadSequence();
        var oidData = encap.ReadEncodedValue().ToArray();
        var resto = new List<byte>();
        while (signedData.HasData)
        {
            resto.AddRange(signedData.ReadEncodedValue().ToArray());
        }

        static byte[] Lunghezza(int n) => n < 0x80 ? [(byte)n] : [0x82, (byte)(n >> 8), (byte)n];
        var blocchi = xml.Chunk(blocco).SelectMany(b => (byte[])[0x04, .. Lunghezza(b.Length), .. b]);
        return
        [
            0x30, 0x80, .. oidSignedData,
            0xA0, 0x80, 0x30, 0x80, .. versione, .. algoritmi,
            0x30, 0x80, .. oidData, 0xA0, 0x80, 0x24, 0x80, .. blocchi, 0, 0, 0, 0, 0, 0,
            .. resto, 0, 0, 0, 0,
            0, 0,
        ];
    }

    [Fact]
    public void P7mFirmato_SiEstraeLXml()
    {
        var xml = FatturaFornitore();
        Assert.Equal(xml, LettoreXmlSdi.EstraiXmlFattura(P7mFirmato(xml)));
        Assert.Equal(xml, LettoreXmlSdi.EstraiXmlFattura(Encoding.ASCII.GetBytes(Convert.ToBase64String(P7mFirmato(xml), Base64FormattingOptions.InsertLineBreaks))));
    }

    /// <summary>Il caso che il ritaglio da solo non regge: le intestazioni dei blocchi finirebbero dentro l'XML.</summary>
    [Fact]
    public void P7mABlocchi_SiRiunisceIlContenuto()
    {
        var xml = FatturaFornitore();
        Assert.Equal(xml, LettoreXmlSdi.EstraiXmlFattura(P7mABlocchi(xml, 100)));
    }

    private sealed class Ricezione
    {
        public readonly SdiFinto Sdi = new();
        public readonly ArchivioFinto Archivio = new();
        public readonly NotificheFinte Notifiche = new();

        public SdiRicezioneService Servizio() => new(Sdi, Archivio, new StruttureFinte(), new NotificaService(Notifiche, null!, null!, null!, null!, null!), new LogFinto());

        public Task<bool> Ricevi(byte[] file, string identificativo = "900", bool prova = false) =>
            Servizio().RiceviAsync(identificativo, "IT09876543210_00001.xml.p7m", file, null, null, prova, CancellationToken.None);
    }

    [Fact]
    public async Task FatturaFirmata_UnaStruttura_SiRegistraESiAvvisa()
    {
        var r = new Ricezione();
        var strutturaId = Guid.NewGuid();
        r.Sdi.Strutture["01234567890"] = [new(strutturaId, Guid.NewGuid())];

        Assert.True(await r.Ricevi(P7m(FatturaFornitore())));

        var fattura = Assert.Single(r.Sdi.FattureRicevute);
        Assert.Equal(strutturaId, fattura.StrutturaId);
        Assert.Equal("Lavanderia Industriale S.r.l.", fattura.Fornitore);
        Assert.Equal("A/1", fattura.Numero);
        Assert.Equal(122.00m, fattura.ImportoTotale);
        Assert.Equal(22.00m, fattura.Imposta);
        Assert.Equal(EsitoFileRicevutoSdi.Importata, Assert.Single(r.Sdi.FileRicevuti).Esito);
        Assert.Single(r.Archivio.Salvati);
        Assert.Equal(TipoNotifica.FatturaRicevuta, Assert.Single(r.Notifiche.Elenco).Tipo);
    }

    /// <summary>Fatturazione differita: un file, più documenti, una fattura ricevuta per ognuno.</summary>
    [Fact]
    public async Task FileConPiuDocumenti_UnaFatturaPerDocumento()
    {
        var r = new Ricezione();
        r.Sdi.Strutture["01234567890"] = [new(Guid.NewGuid(), Guid.NewGuid())];

        await r.Ricevi(FatturaFornitore(documenti: 3));

        Assert.Equal(["A/1", "A/2", "A/3"], r.Sdi.FattureRicevute.Select(f => f.Numero));
    }

    /// <summary>Le fatture ricevute sono del Cliente: due strutture sue con la stessa partita IVA non sono un problema.</summary>
    [Fact]
    public async Task DueStruttureDelloStessoCliente_FatturaRegistrata_AvvisateEntrambe()
    {
        var r = new Ricezione();
        var cliente = Guid.NewGuid();
        var (prima, seconda) = (Guid.NewGuid(), Guid.NewGuid());
        r.Sdi.Strutture["01234567890"] = [new(prima, cliente), new(seconda, cliente)];

        Assert.True(await r.Ricevi(FatturaFornitore()));

        Assert.Equal(prima, Assert.Single(r.Sdi.FattureRicevute).StrutturaId);
        Assert.Equal(EsitoFileRicevutoSdi.Importata, Assert.Single(r.Sdi.FileRicevuti).Esito);
        Assert.Equal([prima, seconda], r.Notifiche.Elenco.Select(n => n.StrutturaId));
    }

    [Theory]
    [InlineData(0, EsitoFileRicevutoSdi.SenzaDestinatario)]
    [InlineData(2, EsitoFileRicevutoSdi.PiuDestinatari)]
    public async Task NessunClienteOClientiDiversi_Quarantena(int clienti, EsitoFileRicevutoSdi atteso)
    {
        var r = new Ricezione();
        r.Sdi.Strutture["01234567890"] = Enumerable.Range(0, clienti).Select(_ => new StrutturaDestinataria(Guid.NewGuid(), Guid.NewGuid())).ToList();

        Assert.True(await r.Ricevi(FatturaFornitore()));

        var file = Assert.Single(r.Sdi.FileRicevuti);
        Assert.Equal(atteso, file.Esito);
        Assert.Null(file.StrutturaId);
        Assert.Equal("IT01234567890", file.IdentificativoCessionario);
        Assert.Empty(r.Sdi.FattureRicevute);
    }

    [Fact]
    public async Task FatturaDiCollaudo_NonDiventaUnAcquisto()
    {
        var r = new Ricezione();
        r.Sdi.Strutture["01234567890"] = [new(Guid.NewGuid(), Guid.NewGuid())];

        Assert.True(await r.Ricevi(FatturaFornitore(), prova: true));

        Assert.Equal(EsitoFileRicevutoSdi.Prova, Assert.Single(r.Sdi.FileRicevuti).Esito);
        Assert.Empty(r.Sdi.FattureRicevute);
        Assert.Empty(r.Notifiche.Elenco);
    }

    /// <summary>Lo SdI ritenta se non riceve la risposta in tempo: la seconda consegna non fa una seconda fattura.</summary>
    [Fact]
    public async Task ConsegnaRipetuta_UnaSolaRiga()
    {
        var r = new Ricezione();
        r.Sdi.Strutture["01234567890"] = [new(Guid.NewGuid(), Guid.NewGuid())];

        Assert.True(await r.Ricevi(FatturaFornitore()));
        Assert.True(await r.Ricevi(FatturaFornitore()));

        Assert.Single(r.Sdi.FileRicevuti);
        Assert.Single(r.Sdi.FattureRicevute);
    }

    /// <summary>Senza copia su disco niente presa in carico: lo SdI ritenterà, il documento non si perde.</summary>
    [Fact]
    public async Task DiscoInErrore_NessunaPresaInCarico()
    {
        var r = new Ricezione();
        r.Archivio.Guasto = true;

        Assert.False(await r.Ricevi(FatturaFornitore()));
        Assert.Empty(r.Sdi.FileRicevuti);
    }

    /// <summary>Illeggibile ma al sicuro: presa in carico data, file conservato per recuperarlo.</summary>
    [Fact]
    public async Task FileIlleggibile_ConservatoEPresoInCarico()
    {
        var r = new Ricezione();

        Assert.True(await r.Ricevi(Encoding.UTF8.GetBytes("non è una fattura")));

        Assert.Equal(EsitoFileRicevutoSdi.NonLeggibile, Assert.Single(r.Sdi.FileRicevuti).Esito);
    }

    // ------------------------------------------------------------------ finti

    private sealed class SdiFinto : ISdiRepository
    {
        public List<TrasmissioneFattura> Trasmissioni { get; } = [];
        public List<RicevutaSdi> Ricevute { get; } = [];
        public List<FileRicevutoSdi> FileRicevuti { get; } = [];
        public List<FatturaRicevuta> FattureRicevute { get; } = [];
        public Dictionary<string, List<StrutturaDestinataria>> Strutture { get; } = [];

        public Task<TrasmissioneFattura?> TrasmissionePerNomeFileAsync(string nomeFile, CancellationToken cancellationToken) => Task.FromResult(Trasmissioni.FirstOrDefault(t => t.NomeFile == nomeFile));
        public Task<TrasmissioneFattura?> TrasmissionePerIdentificativoAsync(string identificativoSdi, CancellationToken cancellationToken) => Task.FromResult(Trasmissioni.FirstOrDefault(t => t.IdentificativoSdi == identificativoSdi));
        public Task AggiungiRicevutaAsync(RicevutaSdi ricevuta, CancellationToken cancellationToken) { Ricevute.Add(ricevuta); return Task.CompletedTask; }
        public Task<bool> FileRicevutoEsisteAsync(string identificativoSdi, bool prova, CancellationToken cancellationToken) => Task.FromResult(FileRicevuti.Any(f => f.IdentificativoSdi == identificativoSdi && f.Prova == prova));
        public Task AggiungiFileRicevutoAsync(FileRicevutoSdi file, CancellationToken cancellationToken) { FileRicevuti.Add(file); return Task.CompletedTask; }
        public Task<bool> FatturaRicevutaEsisteAsync(IReadOnlyCollection<Guid> strutturaIds, string impronta, CancellationToken cancellationToken) => Task.FromResult(FattureRicevute.Any(f => strutturaIds.Contains(f.StrutturaId) && f.Impronta == impronta));
        public Task AggiungiFattureRicevuteAsync(IReadOnlyList<FatturaRicevuta> fatture, CancellationToken cancellationToken) { FattureRicevute.AddRange(fatture); return Task.CompletedTask; }
        public Task<IReadOnlyList<StrutturaDestinataria>> StrutturePerIdentificativoFiscaleAsync(string? partitaIva, string? codiceFiscale, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StrutturaDestinataria>>(Strutture.TryGetValue(FileSdiService.Normalizza(partitaIva) ?? "", out var trovate) ? trovate : []);
        public Task SalvaAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ArchivioFinto : IArchivioSdi
    {
        public bool Guasto { get; set; }
        public List<string> Salvati { get; } = [];

        public Task SalvaAsync(string identificativoSdi, bool prova, string nomeFile, byte[] file, string? nomeFileMetadati, byte[]? metadati, CancellationToken cancellationToken)
        {
            if (Guasto)
            {
                throw new IOException("disco pieno");
            }

            Salvati.Add(identificativoSdi);
            return Task.CompletedTask;
        }
    }

    private sealed class FattureFinte : IDatiFatturaRepository
    {
        public Dictionary<Guid, DatiFattura> Elenco { get; } = [];
        public Task<DatiFattura?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Elenco.GetValueOrDefault(id));
        public Task<DatiFattura?> GetByPrenotazioneIdAsync(Guid prenotazioneId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DatiFattura>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> ListaAnniConDatiAsync(Guid strutturaId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetMaxProgressivoAsync(Guid strutturaId, int anno, TipoEmissioneDocumento tipoEmissione, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> TryAddAsync(DatiFattura entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(DatiFattura entity, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SostituisciRigheAsync(DatiFattura entity, List<RigaFattura> righe, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<decimal> TotaleStornatoAsync(Guid fatturaId, Guid? escludiNotaId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<RigaFatturata>> ListRigheFatturateAsync(Guid strutturaId, IReadOnlyCollection<Guid> prenotazioneIds, Guid? escludiFatturaId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NotificheFinte : INotificaRepository
    {
        public List<Notifica> Elenco { get; } = [];
        public Task<bool> EsisteChiaveDedupAsync(Guid strutturaId, string chiaveDedup, CancellationToken cancellationToken) => Task.FromResult(Elenco.Any(n => n.StrutturaId == strutturaId && n.ChiaveDedup == chiaveDedup));
        public Task AddAsync(Notifica notifica, CancellationToken cancellationToken) { Elenco.Add(notifica); return Task.CompletedTask; }
        public Task<Notifica?> GetAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Notifica>> ListaAsync(Guid strutturaId, bool soloNonLette, bool vedeStatoCamera, bool vedeTicket, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> ContaNonLetteAsync(Guid strutturaId, bool vedeStatoCamera, bool vedeTicket, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> EsistePerPrenotazioneAsync(Guid strutturaId, TipoNotifica tipo, Guid prenotazioneId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> EsisteArrivoPerPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Notifica>> ListCancellazioniPendentiAsync(Guid strutturaId, string canale, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Notifica>> ListCancellazioniPendentiScaduteAsync(Guid strutturaId, DateTime adesso, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SegnaTutteLetteAsync(Guid strutturaId, bool vedeStatoCamera, bool vedeTicket, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SegnaLettePerPrenotazioneAsync(Guid strutturaId, TipoNotifica tipo, Guid prenotazioneId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Notifica notifica, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StruttureFinte : IStrutturaRepository
    {
        public Task<Guid?> GetClienteIdAsync(Guid strutturaId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
        public Task<Struttura?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Struttura?>(new Struttura { Id = id });
        public Task<IReadOnlyList<Struttura>> ListByClienteAsync(Guid? clienteId, bool includiInattive, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Struttura>>([]);
        public Task<IReadOnlyList<Struttura>> ListAssegnateAsync(Guid utenteId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(Struttura s, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Struttura s, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    // ------------------------------------------------------------------ quarantena (Super Admin)

    private static CanaleSdiAdminService Admin(Ricezione r) =>
        new(new AdminFinto(r.Sdi), new CanaleFinto(), r.Servizio(), new StruttureFinte(), new LogFinto());

    [Fact]
    public async Task Quarantena_AttribuitaAMano_DiventaFatturaRicevuta()
    {
        var r = new Ricezione();
        await r.Ricevi(FatturaFornitore());
        var file = Assert.Single(r.Sdi.FileRicevuti);
        Assert.Equal(EsitoFileRicevutoSdi.SenzaDestinatario, file.Esito);
        var strutturaId = Guid.NewGuid();

        await Admin(r).AttribuisciAsync(new Utente(superAdmin: true), file.Id, strutturaId, CancellationToken.None);

        Assert.Equal(strutturaId, Assert.Single(r.Sdi.FattureRicevute).StrutturaId);
        Assert.Equal(strutturaId, file.StrutturaId);
        Assert.Equal(EsitoFileRicevutoSdi.Importata, file.Esito);
        await Assert.ThrowsAsync<ConflictException>(() => Admin(r).AttribuisciAsync(new Utente(superAdmin: true), file.Id, strutturaId, CancellationToken.None));
    }

    [Fact]
    public async Task Quarantena_NonLeggibileODiCollaudo_NonSiAttribuisce()
    {
        var r = new Ricezione();
        await r.Ricevi(Encoding.UTF8.GetBytes("non è una fattura"), identificativo: "901");
        await r.Ricevi(FatturaFornitore(), identificativo: "902", prova: true);

        foreach (var file in r.Sdi.FileRicevuti)
        {
            await Assert.ThrowsAsync<ConflictException>(() => Admin(r).AttribuisciAsync(new Utente(superAdmin: true), file.Id, Guid.NewGuid(), CancellationToken.None));
        }

        Assert.Empty(r.Sdi.FattureRicevute);
    }

    [Fact]
    public async Task Quarantena_SoloIlSuperAdmin()
    {
        var r = new Ricezione();
        await r.Ricevi(FatturaFornitore());

        await Assert.ThrowsAsync<ForbiddenException>(() => Admin(r).StatoAsync(new Utente(superAdmin: false), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Admin(r).AttribuisciAsync(new Utente(superAdmin: false), r.Sdi.FileRicevuti[0].Id, Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(r.Sdi.FattureRicevute);
    }

    private sealed class Utente(bool superAdmin) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UtenteId => Guid.Empty;
        public string Email => "operatore@example.com";
        public bool IsSuperAdmin => superAdmin;
        public Guid? ClienteId => null;
        public bool IsClienteAccount => false;
    }

    private sealed class CanaleFinto : IConfigurazioneCanaleSdi
    {
        public string? CodiceFiscaleTrasmittente => null;
        public int ProgressivoIniziale => 1000;
        public string? CodiceDestinatario => null;
        public bool Collaudo => true;
        public bool CollaudoApplicaEsiti { get; set; }
    }

    private sealed class AdminFinto(SdiFinto sdi) : ICanaleSdiAdminRepository
    {
        public Task<IReadOnlyList<FileRicevutoSdi>> FileInQuarantenaAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FileRicevutoSdi>>(sdi.FileRicevuti.Where(f => f.StrutturaId is null && !f.Prova).ToList());
        public Task<IReadOnlyList<FileRicevutoSdi>> FileDiCollaudoAsync(int massimo, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FileRicevutoSdi>>(sdi.FileRicevuti.Where(f => f.Prova).ToList());
        public Task<IReadOnlyList<RicevutaSdi>> RicevuteNonAbbinateAsync(int massimo, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RicevutaSdi>>(sdi.Ricevute.Where(x => x.TrasmissioneFatturaId is null).ToList());
        public Task<FileRicevutoSdi?> FileRicevutoAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(sdi.FileRicevuti.FirstOrDefault(f => f.Id == id));
        public Task<RicevutaSdi?> RicevutaAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(sdi.Ricevute.FirstOrDefault(x => x.Id == id));
    }

    private sealed class LogFinto : ILogEventoService
    {
        public Task RegistraAsync(LivelloLog livello, string messaggio, string origine, string? dettaglio = null, string? correlationId = null,
            Guid? clienteId = null, Guid? strutturaId = null, string? categoria = null, string? operatore = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PagedResult<LogEvento>> CercaAsync(LogEventoFiltro filtro, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> PulisciVecchiAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
