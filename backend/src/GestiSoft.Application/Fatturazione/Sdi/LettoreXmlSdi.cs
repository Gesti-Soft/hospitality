using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione.Sdi;

/// <summary>
/// Lettura dei messaggi dello SdI e delle fatture dei fornitori. Sempre per nome locale degli
/// elementi, mai con classi generate dagli schemi: i messaggi sono di sei tipi con campi diversi,
/// contengono una firma XAdES, e le fatture le scrive chiunque, in versioni diverse del tracciato.
/// Legarsi allo schema vorrebbe dire rompersi al primo campo inatteso.
/// </summary>
public static class LettoreXmlSdi
{
    /// <summary>XML da byte, senza DTD né risoluzione di risorse esterne. Null se non è XML.</summary>
    public static XElement? Radice(byte[] xml)
    {
        try
        {
            var impostazioni = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new MemoryStream(xml), impostazioni);
            return XDocument.Load(reader).Root;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>Il primo discendente con quel nome locale, senza entrare nella firma.</summary>
    public static XElement? Trova(XElement da, string nome) =>
        da.Descendants().FirstOrDefault(e => e.Name.LocalName == nome && !DentroFirma(e));

    public static string? Testo(XElement? da, string nome) =>
        da is null ? null : Trova(da, nome)?.Value.Trim() is { Length: > 0 } t ? t : null;

    /// <summary>Figlio diretto: dove lo stesso nome compare più in basso con un altro significato.</summary>
    public static string? Figlio(XElement? da, string nome) =>
        da?.Elements().FirstOrDefault(e => e.Name.LocalName == nome)?.Value.Trim() is { Length: > 0 } t ? t : null;

    private static bool DentroFirma(XElement e) => e.Ancestors().Any(a => a.Name.LocalName == "Signature");

    // ------------------------------------------------------------------ ricevute

    /// <summary>
    /// Il tipo si riconosce dall'elemento radice, e i nomi dello SdI non sono regolari: lo stesso
    /// messaggio si chiama Ricevuta… da una parte e Notifica… dall'altra (lo scarto è NotificaScarto
    /// nei file del portale e RicevutaScarto dal canale). Si toglie il prefisso e si guarda il resto:
    /// una lista fissa di nomi ha fatto ignorare in silenzio uno scarto vero.
    /// </summary>
    public static TipoRicevutaSdi TipoRicevuta(string radice)
    {
        var resto = radice.StartsWith("Ricevuta", StringComparison.Ordinal) ? radice["Ricevuta".Length..]
            : radice.StartsWith("Notifica", StringComparison.Ordinal) ? radice["Notifica".Length..]
            : radice;
        return resto switch
        {
            "Consegna" => TipoRicevutaSdi.Consegna,
            "ImpossibilitaRecapito" or "MancataConsegna" => TipoRicevutaSdi.MancataConsegna,
            "Scarto" => TipoRicevutaSdi.Scarto,
            "DecorrenzaTermini" => TipoRicevutaSdi.DecorrenzaTermini,
            "AttestazioneTrasmissioneFattura" => TipoRicevutaSdi.AttestazioneTrasmissione,
            "Esito" or "EsitoCommittente" => TipoRicevutaSdi.Esito,
            _ => TipoRicevutaSdi.Sconosciuta,
        };
    }

    public record ErroreSdi(string? Codice, string? Descrizione, string? Suggerimento);

    public record RicevutaLetta(
        TipoRicevutaSdi Tipo,
        string? IdentificativoSdi,
        string? NomeFile,
        DateTimeOffset? DataOra,
        string? Descrizione,
        IReadOnlyList<ErroreSdi> Errori,
        string? Note)
    {
        /// <summary>Esito leggibile: per lo scarto i codici con la loro descrizione, altrimenti descrizione e note.</summary>
        public string? Testo => Errori.Count > 0
            ? string.Join("; ", Errori.Select(e => $"{e.Codice} {e.Descrizione}{(e.Suggerimento is null ? "" : $" ({e.Suggerimento})")}".Trim()))
            : string.Join(" - ", new[] { Descrizione, Note }.Where(t => !string.IsNullOrWhiteSpace(t))) is { Length: > 0 } t ? t : null;
    }

    public static RicevutaLetta? LeggiRicevuta(byte[] xml)
    {
        var radice = Radice(xml);
        if (radice is null)
        {
            return null;
        }

        var tipo = TipoRicevuta(radice.Name.LocalName);
        // La Descrizione dell'esito è figlia diretta della radice: ce n'è un'altra dentro
        // <Destinatario> che descrive il canale di recapito, e presa a caso finiva sulla fattura.
        // Fa eccezione l'esito del committente, dove sta dentro <EsitoCommittente>.
        var descrizione = tipo == TipoRicevutaSdi.Esito
            ? Testo(radice.Elements().FirstOrDefault(e => e.Name.LocalName == "EsitoCommittente"), "Descrizione")
            : Figlio(radice, "Descrizione");

        var errori = radice.Elements().FirstOrDefault(e => e.Name.LocalName == "ListaErrori")?
            .Elements().Where(e => e.Name.LocalName == "Errore")
            .Select(e => new ErroreSdi(Figlio(e, "Codice"), RiparaDoppiaCodifica(Figlio(e, "Descrizione")), RiparaDoppiaCodifica(Figlio(e, "Suggerimento"))))
            .ToList() ?? [];

        var dataOra = Figlio(radice, "DataOraConsegna") ?? Figlio(radice, "DataOraRicezione");
        return new RicevutaLetta(
            tipo,
            Figlio(radice, "IdentificativoSdI"),
            Figlio(radice, "NomeFile"),
            DateTimeOffset.TryParse(dataOra, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null,
            RiparaDoppiaCodifica(descrizione),
            errori,
            RiparaDoppiaCodifica(Figlio(radice, "Note")));
    }

    /// <summary>
    /// Testo codificato due volte («Ã¨» al posto di «è»), frequente nelle ricevute scaricate dal
    /// portale: un carattere 0xC2/0xC3 seguito da uno fra 0x80 e 0xBF è una sequenza UTF-8 letta come
    /// Latin-1. Si ripara solo se ci sono coppie del genere, altrimenti il testo resta com'è.
    /// </summary>
    public static string? RiparaDoppiaCodifica(string? testo)
    {
        if (string.IsNullOrEmpty(testo) || !testo.Zip(testo.Skip(1)).Any(c => c.First is 'Â' or 'Ã' && c.Second is >= '\u0080' and <= '¿'))
        {
            return testo;
        }

        try
        {
            return new UTF8Encoding(false, true).GetString(Encoding.Latin1.GetBytes(testo));
        }
        catch (DecoderFallbackException)
        {
            return testo;
        }
    }

    // ------------------------------------------------------------------ fatture ricevute

    /// <summary>
    /// L'XML della fattura da un file ricevuto: in chiaro, oppure firmato CAdES (.p7m), anche in
    /// base64. Il .p7m si apre come CMS: il contenuto firmato può essere spezzato in più blocchi
    /// (OCTET STRING costruita), con le intestazioni DER in mezzo all'XML. Se non si decodifica come
    /// CMS si ritaglia dal tag di apertura all'ultimo di chiusura. La firma l'ha già verificata lo SdI.
    /// </summary>
    public static byte[]? EstraiXmlFattura(byte[] file)
    {
        if (ContenutoCms(file) is { } firmato && Ritaglia(firmato) is { } contenuto)
        {
            return contenuto;
        }

        if (Ritaglia(file) is { } xml)
        {
            return xml;
        }

        // .p7m "armored": il DER intero in base64, a volte con le righe BEGIN/END.
        var testo = Encoding.ASCII.GetString(file);
        var base64 = string.Concat(testo.Split('\n').Where(r => !r.StartsWith("-----", StringComparison.Ordinal)).Select(r => r.Trim()));
        byte[] der;
        try
        {
            der = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }

        return (ContenutoCms(der) is { } c ? Ritaglia(c) : null) ?? Ritaglia(der);
    }

    /// <summary>Il contenuto di un involucro CMS SignedData, già riunito se era a blocchi. Null se non è CMS.</summary>
    private static byte[]? ContenutoCms(byte[] dati)
    {
        if (dati.Length == 0 || dati[0] != 0x30)
        {
            return null;
        }

        try
        {
            var cms = new SignedCms();
            cms.Decode(dati);
            return cms.ContentInfo.Content;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static byte[]? Ritaglia(byte[] dati)
    {
        var testo = Encoding.Latin1.GetString(dati);
        var apertura = testo.IndexOf("FatturaElettronica ", StringComparison.Ordinal) is var i and >= 0 ? i
            : testo.IndexOf("FatturaElettronica>", StringComparison.Ordinal);
        if (apertura < 0)
        {
            return null;
        }

        var inizio = testo.LastIndexOf('<', apertura);
        var chiusura = testo.LastIndexOf("FatturaElettronica>", StringComparison.Ordinal);
        var fine = chiusura < 0 ? -1 : testo.IndexOf('>', chiusura);
        if (inizio < 0 || fine <= inizio)
        {
            return null;
        }

        // Dichiarazione XML davanti, se c'era: dice la codifica del contenuto.
        var dichiarazione = testo.LastIndexOf("<?xml", inizio, StringComparison.Ordinal);
        if (dichiarazione >= 0 && testo.IndexOf("?>", dichiarazione, StringComparison.Ordinal) < inizio)
        {
            inizio = dichiarazione;
        }

        var xml = dati.AsSpan(inizio, fine - inizio + 1).ToArray();
        return Radice(xml) is null ? null : xml;
    }

    public record DocumentoRicevuto(
        int Indice,
        string TipoDocumento,
        string Numero,
        DateOnly Data,
        string Divisa,
        decimal? ImportoTotale,
        decimal Imponibile,
        decimal Imposta);

    public record FatturaLetta(
        string Fornitore,
        string? FornitorePartitaIva,
        string? FornitoreCodiceFiscale,
        string? CessionarioPartitaIva,
        string? CessionarioCodiceFiscale,
        IReadOnlyList<DocumentoRicevuto> Documenti);

    /// <summary>
    /// Intestazione e documenti di una fattura ricevuta. FatturaElettronicaBody è ripetibile
    /// (fatturazione differita: un'intestazione, N documenti): si legge una lista.
    /// </summary>
    public static FatturaLetta? LeggiFattura(byte[] xml)
    {
        var radice = Radice(xml);
        var header = radice?.Elements().FirstOrDefault(e => e.Name.LocalName == "FatturaElettronicaHeader");
        if (radice is null || header is null)
        {
            return null;
        }

        var cedente = header.Elements().FirstOrDefault(e => e.Name.LocalName == "CedentePrestatore");
        var cessionario = header.Elements().FirstOrDefault(e => e.Name.LocalName == "CessionarioCommittente");
        var anagraficaCedente = Trova(cedente ?? header, "Anagrafica");
        var fornitore = Testo(anagraficaCedente, "Denominazione")
            ?? $"{Testo(anagraficaCedente, "Nome")} {Testo(anagraficaCedente, "Cognome")}".Trim();

        var documenti = radice.Elements().Where(e => e.Name.LocalName == "FatturaElettronicaBody")
            .Select((body, i) =>
            {
                var generali = Trova(body, "DatiGeneraliDocumento");
                var riepiloghi = body.Descendants().Where(e => e.Name.LocalName == "DatiRiepilogo").ToList();
                return new DocumentoRicevuto(
                    i + 1,
                    Testo(generali, "TipoDocumento") ?? "",
                    Testo(generali, "Numero") ?? "",
                    DateOnly.TryParse(Testo(generali, "Data"), CultureInfo.InvariantCulture, out var data) ? data : default,
                    Testo(generali, "Divisa") ?? "EUR",
                    Importo(Testo(generali, "ImportoTotaleDocumento")),
                    riepiloghi.Sum(r => Importo(Figlio(r, "ImponibileImporto")) ?? 0),
                    riepiloghi.Sum(r => Importo(Figlio(r, "Imposta")) ?? 0));
            })
            .ToList();

        return new FatturaLetta(
            string.IsNullOrWhiteSpace(fornitore) ? "Fornitore senza nome" : fornitore,
            IdFiscaleIva(cedente),
            Testo(Trova(cedente ?? header, "DatiAnagrafici"), "CodiceFiscale"),
            IdFiscaleIva(cessionario),
            Testo(Trova(cessionario ?? header, "DatiAnagrafici"), "CodiceFiscale"),
            documenti);
    }

    private static string? IdFiscaleIva(XElement? soggetto)
    {
        var id = soggetto is null ? null : Trova(soggetto, "IdFiscaleIVA");
        return Testo(id, "IdCodice") is { } codice ? $"{Testo(id, "IdPaese") ?? "IT"}{codice}" : null;
    }

    private static decimal? Importo(string? testo) =>
        decimal.TryParse(testo, NumberStyles.Number, CultureInfo.InvariantCulture, out var valore) ? valore : null;
}
