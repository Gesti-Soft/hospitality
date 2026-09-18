using System.Globalization;
using System.Xml.Linq;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestiSoft.Infrastructure.Fatturazione;

/// <summary>
/// Genera PDF e XML SDI di una fattura al volo (mai persistiti, vedi
/// <see cref="IFatturaDocumentGenerator"/>). Il PDF è un layout nuovo, semplice; l'XML segue la
/// struttura standard "FatturaElettronica ordinaria" (namespace SDI v1.2, FormatoTrasmissione
/// FPR12 verso privati, stessa scelta del legacy) scritta a mano con System.Xml.Linq — non è
/// validata contro l'XSD ufficiale (nessuna libreria di validazione portata, coerente con lo
/// scope Fase 4: generazione locale per download manuale, nessun invio a SDI).
/// </summary>
public class FatturaDocumentGenerator : IFatturaDocumentGenerator
{
    /// <summary>Colori presi dai token del frontend, così il documento non sembra di un altro prodotto.</summary>
    private const string Inchiostro = "#1B222C";
    private const string InchiostroTenue = "#5B6472";
    private const string Filetto = "#D7DCE3";

    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    public byte[] GeneraPdf(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda, string? nomeStruttura)
    {
        var intestazione = PrimoNonVuoto(nomeStruttura, azienda?.Denominazione, $"{azienda?.Nome} {azienda?.Cognome}") ?? "Fattura";
        var ricevuta = fattura.TipoEmissione == TipoEmissioneDocumento.Ricevuta;
        var imposta = Math.Round(fattura.ImportoTotale - fattura.PrezzoTotale - (fattura.ImpostaSoggiorno ?? 0), 2, MidpointRounding.AwayFromZero);

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Inchiostro));

                page.Header().Column(testata =>
                {
                    testata.Item().Row(row =>
                    {
                        // A sinistra chi emette: il nome con cui la struttura è conosciuta, e sotto,
                        // in piccolo, l'identità fiscale che vale davanti al fisco.
                        row.RelativeItem().Column(c =>
                        {
                            // Il logo sta sopra e non al posto del nome: non tutti i loghi contengono
                            // il nome della struttura, e una fattura che non dice chi l'ha emessa
                            // sarebbe un documento peggiore. Riquadro massimo, proporzioni rispettate:
                            // un logo largo o alto si adatta invece di sfondare la testata.
                            if (azienda?.Logo is { Length: > 0 } logo)
                            {
                                c.Item().PaddingBottom(8).MaxWidth(190).MaxHeight(45).Image(logo).FitArea();
                            }

                            c.Item().Text(intestazione).FontSize(19).SemiBold();
                            foreach (var riga in RigheEmittente(azienda))
                            {
                                c.Item().PaddingTop(1).Text(riga).FontSize(8.5f).FontColor(InchiostroTenue);
                            }
                        });

                        // A destra l'identità del documento: è il dato che si cerca per primo quando
                        // una fattura va ritrovata.
                        row.ConstantItem(170).Column(c =>
                        {
                            c.Item().AlignRight().Text(EtichettaTipoDocumento(fattura)).FontSize(11).SemiBold();
                            c.Item().AlignRight().PaddingTop(2).Text($"n. {fattura.NumeroDocumento} / {fattura.Anno}").FontSize(14).SemiBold();
                            c.Item().AlignRight().PaddingTop(1).Text(fattura.DataDocumento.ToString("d MMMM yyyy", Italiano)).FontSize(9).FontColor(InchiostroTenue);
                        });
                    });

                    testata.Item().PaddingTop(12).LineHorizontal(1).LineColor(Filetto);
                });

                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Spacing(18);

                    col.Item().Column(c =>
                    {
                        c.Item().Text(fattura.TipoEmissione == TipoEmissioneDocumento.Ricevuta ? "Ricevuta rilasciata a" : "Fatturato a").FontSize(8.5f).FontColor(InchiostroTenue);
                        c.Item().PaddingTop(2).Text(NomeCliente(cliente)).FontSize(11.5f).SemiBold();
                        foreach (var riga in RigheCliente(cliente))
                        {
                            c.Item().PaddingTop(1).Text(riga).FontSize(9).FontColor(InchiostroTenue);
                        }
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5);
                            columns.ConstantColumn(50);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(40);
                            columns.ConstantColumn(85);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellaTestata).Text("Descrizione");
                            header.Cell().Element(CellaTestata).AlignRight().Text("Quantità");
                            header.Cell().Element(CellaTestata).AlignRight().Text("Prezzo");
                            header.Cell().Element(CellaTestata).AlignRight().Text(ricevuta ? "" : "IVA");
                            header.Cell().Element(CellaTestata).AlignRight().Text("Importo");
                        });

                        table.Cell().Element(CellaRiga).Text(fattura.Descrizione ?? "—");
                        table.Cell().Element(CellaRiga).AlignRight().Text(fattura.Quantita.ToString("0.##", Italiano));
                        table.Cell().Element(CellaRiga).AlignRight().Text(Valuta(fattura.PrezzoUnitario));
                        table.Cell().Element(CellaRiga).AlignRight().Text(ricevuta ? "" : fattura.AliquotaIva is { } iva ? $"{(int)iva}%" : "—");
                        table.Cell().Element(CellaRiga).AlignRight().Text(Valuta(fattura.PrezzoTotale));

                        // Riga a sé anche sulla carta: chi legge deve vedere che quella somma non ha
                        // IVA e perché, non trovarsela confusa nel prezzo del soggiorno.
                        if (fattura.ImpostaSoggiorno is { } tassa and > 0)
                        {
                            table.Cell().Element(CellaRiga).Text("Imposta di soggiorno");
                            table.Cell().Element(CellaRiga).AlignRight().Text("1");
                            table.Cell().Element(CellaRiga).AlignRight().Text(Valuta(tassa));
                            table.Cell().Element(CellaRiga).AlignRight().Text("—");
                            table.Cell().Element(CellaRiga).AlignRight().Text(Valuta(tassa));
                        }
                    });

                    col.Item().AlignRight().Width(230).Column(totali =>
                    {
                        totali.Item().Element(c => RigaTotale(c, ricevuta ? "Corrispettivo" : "Imponibile", Valuta(fattura.PrezzoTotale), false));
                        if (!ricevuta)
                        {
                            totali.Item().Element(c => RigaTotale(c, "Imposta", Valuta(imposta), false));
                        }
                        if (fattura.ImpostaSoggiorno is { } tassaTotale and > 0)
                        {
                            totali.Item().Element(c => RigaTotale(c, "Imposta di soggiorno", Valuta(tassaTotale), false));
                        }

                        totali.Item().PaddingTop(5).BorderTop(1).BorderColor(Filetto).PaddingTop(6)
                            .Element(c => RigaTotale(c, "Totale", Valuta(fattura.ImportoTotale), true));
                    });

                    // La dicitura di legge vince sempre sul codice: il codice natura dice allo SdI
                    // perché l'IVA non c'è, la dicitura lo dice a chi legge la fattura, ed è quella
                    // che il fisco pretende sul documento.
                    if (!string.IsNullOrWhiteSpace(azienda?.DicituraFattura))
                    {
                        col.Item().Text(azienda.DicituraFattura!.Trim()).FontSize(8.5f).FontColor(InchiostroTenue);
                    }
                    else if (fattura.Natura is { } natura)
                    {
                        col.Item().Text($"Operazione non soggetta a IVA — natura {CodiceNatura(natura)}.").FontSize(8.5f).FontColor(InchiostroTenue);
                    }

                    if (ricevuta)
                    {
                        col.Item().Text("Locazione breve — operazione fuori dal campo di applicazione dell'IVA. Documento non fiscale.").FontSize(8.5f).FontColor(InchiostroTenue);
                    }

                    if (fattura.ImpostaSoggiorno is > 0)
                    {
                        col.Item().Text("Imposta di soggiorno esclusa dalla base imponibile ai sensi dell'art. 15 c.1 n.3 DPR 633/72.").FontSize(8.5f).FontColor(InchiostroTenue);
                    }

                    // Va stampata sul documento, non basta il blocco DatiBollo nell'XML.
                    if (fattura.ImportoBollo is > 0)
                    {
                        col.Item().Text(DicituraBollo).FontSize(8.5f).FontColor(InchiostroTenue);
                    }
                });

            });
        });

        return documento.GeneratePdf();
    }

    private static IContainer CellaTestata(IContainer c) =>
        c.BorderBottom(1).BorderColor(Filetto).PaddingBottom(5).DefaultTextStyle(x => x.FontSize(8.5f).SemiBold().FontColor(InchiostroTenue));

    private static IContainer CellaRiga(IContainer c) => c.PaddingVertical(7);

    private static void RigaTotale(IContainer contenitore, string etichetta, string valore, bool forte)
    {
        contenitore.Row(row =>
        {
            row.RelativeItem().Text(etichetta).FontSize(forte ? 11 : 9).FontColor(forte ? Inchiostro : InchiostroTenue);
            row.ConstantItem(110).AlignRight().Text(valore).FontSize(forte ? 13 : 9.5f).SemiBold();
        });
    }

    private static string Valuta(decimal importo) => importo.ToString("C2", Italiano);

    private static string EtichettaTipoDocumento(DatiFattura fattura) =>
        fattura.TipoEmissione == TipoEmissioneDocumento.Ricevuta
            ? "Ricevuta"
            : fattura.TipoDocumento switch
            {
                TipoDocumentoFattura.TD04_NotaDiCredito => "Nota di credito",
                TipoDocumentoFattura.TD06_Parcella => "Parcella",
                _ => "Fattura",
            };

    /// <summary>L'identità fiscale dell'emittente, saltando le righe che non hanno niente da dire.</summary>
    private static IEnumerable<string> RigheEmittente(DatiAziendali? a)
    {
        if (a is null) yield break;

        var ragione = PrimoNonVuoto(a.Denominazione, $"{a.Nome} {a.Cognome}");
        if (ragione is not null) yield return ragione;

        var via = $"{a.Indirizzo} {a.NCivico}".Trim();
        if (via.Length > 0) yield return via;

        var citta = ($"{a.Cap} {a.Comune}".Trim() + (string.IsNullOrWhiteSpace(a.Provincia) ? string.Empty : $" ({a.Provincia})")).Trim();
        if (citta.Length > 0) yield return citta;

        if (!string.IsNullOrWhiteSpace(a.PIva)) yield return $"P. IVA {a.PIva}";
        if (!string.IsNullOrWhiteSpace(a.CodiceFiscale) && a.CodiceFiscale != a.PIva) yield return $"C.F. {a.CodiceFiscale}";
    }

    private static string NomeCliente(DatiCliente? c) =>
        PrimoNonVuoto(c?.Denominazione, $"{c?.Nome} {c?.Cognome}") ?? "—";

    private static IEnumerable<string> RigheCliente(DatiCliente? c)
    {
        if (c is null) yield break;

        var via = $"{c.Indirizzo} {c.NCivico}".Trim();
        if (via.Length > 0) yield return via;

        var citta = ($"{c.Cap} {c.LuogoResidenza}".Trim() + (string.IsNullOrWhiteSpace(c.Provincia) ? string.Empty : $" ({c.Provincia})")).Trim();
        if (citta.Length > 0) yield return citta;

        if (!string.IsNullOrWhiteSpace(c.PIva)) yield return $"P. IVA {c.PIva}";
        if (!string.IsNullOrWhiteSpace(c.CodiceFiscale)) yield return $"C.F. {c.CodiceFiscale}";
    }

    private static string? PrimoNonVuoto(params string?[] valori) =>
        valori.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    public byte[] GeneraXmlSdi(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda)
    {
        XNamespace p = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        var imponibile = fattura.PrezzoTotale;
        var imposta = Math.Round(fattura.ImportoTotale - fattura.PrezzoTotale, 2, MidpointRounding.AwayFromZero);
        var clienteEstero = Estero(cliente?.Iso2);

        var datiTrasmissione = new XElement("DatiTrasmissione",
            new XElement("IdTrasmittente",
                new XElement("IdPaese", CodiceNazione(azienda?.Iso2)),
                new XElement("IdCodice", azienda?.CodiceFiscale ?? azienda?.PIva ?? string.Empty)),
            new XElement("ProgressivoInvio", fattura.Progressivo.ToString("00000", CultureInfo.InvariantCulture)),
            new XElement("FormatoTrasmissione", "FPR12"),
            new XElement("CodiceDestinatario", CodiceDestinatario(cliente, clienteEstero)));

        // La nazione della sede è l'ISO2, non il campo "Nazione" dei dati aziendali: quello è una
        // denominazione leggibile ("Italia") e nel tracciato vale solo il codice a due lettere. Ci
        // finiva davvero, e bastava a impedire la generazione del file.
        var cedentePrestatore = new XElement("CedentePrestatore",
            DatiAnagrafici(azienda?.Iso2, azienda?.PIva, azienda?.CodiceFiscale, azienda?.Denominazione, azienda?.Nome, azienda?.Cognome, azienda?.RegimeFiscale),
            Sede(azienda?.Indirizzo, azienda?.NCivico, azienda?.Cap, azienda?.Comune, azienda?.Provincia, azienda?.Iso2));

        // Per un cessionario non residente l'indirizzo segue le convenzioni dello SDI: CAP fisso a
        // "00000" (il codice postale vero, se serve, va scritto dentro l'Indirizzo) e Provincia
        // omessa, che è valorizzabile solo per l'Italia. La Nazione è quella del cliente: prima era
        // scritta fissa a "IT" e un cliente estero risultava residente in Italia.
        var cessionarioCommittente = new XElement("CessionarioCommittente",
            DatiAnagrafici(cliente?.Iso2, cliente?.PIva, cliente?.CodiceFiscale, cliente?.Denominazione, cliente?.Nome, cliente?.Cognome, regimeFiscale: null),
            Sede(
                cliente?.Indirizzo,
                cliente?.NCivico,
                clienteEstero ? CapEstero : cliente?.Cap,
                cliente?.LuogoResidenza,
                clienteEstero ? null : cliente?.Provincia,
                cliente?.Iso2));

        // L'ordine degli elementi dentro DatiGeneraliDocumento è fissato dallo schema: TipoDocumento,
        // Divisa, Data, Numero, DatiBollo, ... — invertirli fa scartare il file.
        var datiGenerali = new XElement("DatiGenerali",
            new XElement("DatiGeneraliDocumento",
                new XElement("TipoDocumento", fattura.TipoDocumento is { } td ? $"TD{(int)td:00}" : "TD01"),
                new XElement("Divisa", fattura.Divisa ?? "EUR"),
                new XElement("Data", fattura.DataDocumento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new XElement("Numero", fattura.NumeroDocumento),
                fattura.ImportoBollo is { } bollo
                    ? new XElement("DatiBollo",
                        new XElement("BolloVirtuale", "SI"),
                        new XElement("ImportoBollo", bollo.ToString("0.00", CultureInfo.InvariantCulture)))
                    : null));

        var dettaglioLinee = new XElement("DettaglioLinee",
            new XElement("NumeroLinea", 1),
            new XElement("Descrizione", fattura.Descrizione ?? string.Empty),
            new XElement("Quantita", fattura.Quantita.ToString("0.00", CultureInfo.InvariantCulture)),
            new XElement("PrezzoUnitario", fattura.PrezzoUnitario.ToString("0.00000", CultureInfo.InvariantCulture)),
            new XElement("PrezzoTotale", fattura.PrezzoTotale.ToString("0.00", CultureInfo.InvariantCulture)),
            fattura.AliquotaIva is { } aliquota ? new XElement("AliquotaIVA", ((int)aliquota).ToString(CultureInfo.InvariantCulture)) : null,
            fattura.Natura is { } natura ? new XElement("Natura", CodiceNatura(natura)) : null);

        var datiRiepilogo = new XElement("DatiRiepilogo",
            fattura.AliquotaIva is { } aliquotaRiepilogo ? new XElement("AliquotaIVA", ((int)aliquotaRiepilogo).ToString(CultureInfo.InvariantCulture)) : null,
            fattura.Natura is { } naturaRiepilogo ? new XElement("Natura", CodiceNatura(naturaRiepilogo)) : null,
            new XElement("ImponibileImporto", imponibile.ToString("0.00", CultureInfo.InvariantCulture)),
            new XElement("Imposta", imposta.ToString("0.00", CultureInfo.InvariantCulture)),
            new XElement("EsigibilitaIVA", "I"));

        // L'imposta di soggiorno è una somma anticipata in nome e per conto del cliente verso il
        // Comune: esclusa dalla base imponibile ex art. 15 c.1 n.3 DPR 633/72, quindi riga a sé con
        // natura N1 e riepilogo separato, mai sommata all'imponibile del soggiorno — lì dentro
        // pagherebbe un'IVA che non deve.
        var rigaImpostaSoggiorno = fattura.ImpostaSoggiorno is { } impostaSoggiorno and > 0
            ? new XElement("DettaglioLinee",
                new XElement("NumeroLinea", 2),
                new XElement("Descrizione", DescrizioneImpostaSoggiorno),
                new XElement("Quantita", "1.00"),
                new XElement("PrezzoUnitario", impostaSoggiorno.ToString("0.00000", CultureInfo.InvariantCulture)),
                new XElement("PrezzoTotale", impostaSoggiorno.ToString("0.00", CultureInfo.InvariantCulture)),
                new XElement("AliquotaIVA", "0.00"),
                new XElement("Natura", CodiceNatura(NaturaIva.N1_EscluseArt15)))
            : null;

        var riepilogoImpostaSoggiorno = fattura.ImpostaSoggiorno is { } impostaRiepilogo and > 0
            ? new XElement("DatiRiepilogo",
                new XElement("AliquotaIVA", "0.00"),
                new XElement("Natura", CodiceNatura(NaturaIva.N1_EscluseArt15)),
                new XElement("ImponibileImporto", impostaRiepilogo.ToString("0.00", CultureInfo.InvariantCulture)),
                new XElement("Imposta", "0.00"),
                new XElement("EsigibilitaIVA", "I"))
            : null;

        var datiBeniServizi = new XElement("DatiBeniServizi", dettaglioLinee, rigaImpostaSoggiorno, datiRiepilogo, riepilogoImpostaSoggiorno);

        var header = new XElement("FatturaElettronicaHeader", datiTrasmissione, cedentePrestatore, cessionarioCommittente);
        var body = new XElement("FatturaElettronicaBody", datiGenerali, datiBeniServizi);

        var radice = new XElement(p + "FatturaElettronica",
            new XAttribute(XNamespace.Xmlns + "p", p.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),
            new XAttribute("versione", "FPR12"),
            header,
            body);

        var xml = new XDocument(new XDeclaration("1.0", "UTF-8", null), radice);

        using var stream = new MemoryStream();
        xml.Save(stream);
        return stream.ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ValidaPerSdi(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda)
    {
        // Numero, data e importi della fattura non si controllano: sono campi non annullabili del
        // modello, valorizzati alla creazione. Qui manca solo ciò che l'operatore può lasciare in
        // bianco, cioè le anagrafiche.
        var motivi = new List<string>();

        if (azienda is null)
        {
            motivi.Add("mancano i dati fiscali della struttura");
        }
        else
        {
            // Si controlla l'ISO2, che è ciò che finisce davvero nel file sia come identificativo
            // fiscale sia come nazione della sede. Prima qui arrivava azienda.Nazione ("Italia"), e
            // oltre a bloccare la generazione faceva passare la struttura per estera: da lì in poi
            // CAP e Provincia non venivano più controllati, perché per l'estero non si controllano.
            motivi.AddRange(ValidaControparte(
                "della struttura", azienda.Iso2, azienda.PIva, azienda.CodiceFiscale,
                azienda.Denominazione, azienda.Nome, azienda.Cognome,
                azienda.Indirizzo, azienda.Comune, azienda.Cap, azienda.Provincia));

            if (azienda.RegimeFiscale is null)
            {
                motivi.Add("manca il regime fiscale della struttura");
            }
        }

        // Descrizione della riga: obbligatoria nel tracciato. Finora la pretendeva solo il dialogo
        // dell'interfaccia, quindi una fattura creata altrove usciva con l'elemento vuoto e lo SDI
        // la scartava giorni dopo.
        if (string.IsNullOrWhiteSpace(fattura.Descrizione))
        {
            motivi.Add("manca la descrizione della fattura");
        }

        if (cliente is null)
        {
            motivi.Add("la fattura non ha un cliente collegato");
        }
        else
        {
            motivi.AddRange(ValidaControparte(
                "del cliente", cliente.Iso2, cliente.PIva, cliente.CodiceFiscale,
                cliente.Denominazione, cliente.Nome, cliente.Cognome,
                cliente.Indirizzo, cliente.LuogoResidenza, cliente.Cap, cliente.Provincia));
        }

        return motivi;
    }

    /// <summary>
    /// I controlli comuni a chi emette e a chi riceve: sono gli stessi campi, con le stesse regole di
    /// tracciato. CAP e Provincia si controllano solo per l'Italia — per l'estero
    /// <see cref="GeneraXmlSdi"/> scrive comunque le convenzioni ("00000", provincia omessa) e quello
    /// che c'è scritto in anagrafica non finisce nel file.
    /// </summary>
    private static List<string> ValidaControparte(
        string chi, string? nazione, string? pIva, string? codiceFiscale, string? denominazione, string? nome, string? cognome,
        string? indirizzo, string? comune, string? cap, string? provincia)
    {
        var motivi = new List<string>();
        var estero = Estero(nazione);

        if (string.IsNullOrWhiteSpace(pIva) && string.IsNullOrWhiteSpace(codiceFiscale))
        {
            motivi.Add($"manca la partita IVA o il codice fiscale {chi}");
        }

        if (string.IsNullOrWhiteSpace(denominazione) && (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(cognome)))
        {
            motivi.Add($"manca la denominazione {chi}, oppure nome e cognome");
        }

        if (string.IsNullOrWhiteSpace(indirizzo))
        {
            motivi.Add($"manca l'indirizzo {chi}");
        }

        if (string.IsNullOrWhiteSpace(comune))
        {
            motivi.Add($"manca il comune {chi}");
        }

        if (!NazioneValida(nazione))
        {
            motivi.Add($"la nazione {chi} non è un codice di due lettere (es. IT, DE)");
        }

        if (!estero && !CapValido(cap))
        {
            motivi.Add($"il CAP {chi} non è valido: servono cinque cifre");
        }

        if (!estero && !string.IsNullOrWhiteSpace(provincia) && !ProvinciaValida(provincia))
        {
            motivi.Add($"la provincia {chi} non è una sigla di due lettere (es. TP)");
        }

        return motivi;
    }

    /// <summary>Vuoto va bene: <see cref="CodiceNazione"/> ripiega su IT. Altrimenti servono esattamente due lettere.</summary>
    private static bool NazioneValida(string? nazione) =>
        string.IsNullOrWhiteSpace(nazione) || (nazione.Trim().Length == 2 && nazione.Trim().All(char.IsLetter));

    private static bool CapValido(string? cap) =>
        !string.IsNullOrWhiteSpace(cap) && cap.Trim().Length == 5 && cap.Trim().All(char.IsAsciiDigit);

    private static bool ProvinciaValida(string? provincia) =>
        provincia is not null && provincia.Trim().Length == 2 && provincia.Trim().All(char.IsLetter);

    private static XElement DatiAnagrafici(string? iso2, string? pIva, string? codiceFiscale, string? denominazione, string? nome, string? cognome, RegimeFiscale? regimeFiscale)
    {
        var anagrafica = !string.IsNullOrWhiteSpace(denominazione)
            ? new XElement("Anagrafica", new XElement("Denominazione", denominazione))
            : new XElement("Anagrafica", new XElement("Nome", nome ?? string.Empty), new XElement("Cognome", cognome ?? string.Empty));

        return new XElement("DatiAnagrafici",
            !string.IsNullOrWhiteSpace(pIva)
                ? new XElement("IdFiscaleIVA", new XElement("IdPaese", iso2 ?? "IT"), new XElement("IdCodice", pIva))
                : null,
            !string.IsNullOrWhiteSpace(codiceFiscale) ? new XElement("CodiceFiscale", codiceFiscale) : null,
            anagrafica,
            regimeFiscale is { } regime ? new XElement("RegimeFiscale", $"RF{(int)regime:00}") : null);
    }

    private static XElement Sede(string? indirizzo, string? nCivico, string? cap, string? comune, string? provincia, string? nazione) => new(
        "Sede",
        new XElement("Indirizzo", indirizzo ?? string.Empty),
        !string.IsNullOrWhiteSpace(nCivico) ? new XElement("NumeroCivico", nCivico) : null,
        new XElement("CAP", string.IsNullOrWhiteSpace(cap) ? "00000" : cap),
        new XElement("Comune", comune ?? string.Empty),
        !string.IsNullOrWhiteSpace(provincia) ? new XElement("Provincia", provincia.Trim().ToUpperInvariant()) : null,
        new XElement("Nazione", CodiceNazione(nazione)));

    /// <summary>Descrizione della riga dell'imposta di soggiorno: cita la norma perché chi riceve la fattura (e chi la controlla) capisca perché quella somma non ha IVA.</summary>
    private const string DescrizioneImpostaSoggiorno = "Imposta di soggiorno - somma esclusa ex art. 15 c.1 n.3 DPR 633/72";

    /// <summary>Dicitura di legge del bollo assolto in modo virtuale, da stampare sul documento.</summary>
    public const string DicituraBollo = "Imposta di bollo assolta in modo virtuale ai sensi dell'art. 15 del D.P.R. 642/1972 e del D.M. 17/06/2014";

    /// <summary>CAP convenzionale degli indirizzi esteri: il tracciato vuole cinque cifre e per l'estero sono cinque zeri.</summary>
    private const string CapEstero = "00000";

    /// <summary>Codice destinatario di un cessionario non residente: sette "X", convenzione dello SDI.</summary>
    private const string CodiceDestinatarioEstero = "XXXXXXX";

    /// <summary>Codice destinatario di ripiego per un cliente italiano che non ne ha comunicato uno.</summary>
    private const string CodiceDestinatarioItaliano = "0000000";

    /// <summary>
    /// La controparte è estera quando l'ISO2 c'è ed è diverso da IT. Campo vuoto significa italiano:
    /// è lo stesso criterio con cui il frontend decide se calcolare il Codice Fiscale.
    /// </summary>
    private static bool Estero(string? iso2) =>
        !string.IsNullOrWhiteSpace(iso2) && !string.Equals(iso2.Trim(), "IT", StringComparison.OrdinalIgnoreCase);

    private static string CodiceNazione(string? iso2) =>
        string.IsNullOrWhiteSpace(iso2) ? "IT" : iso2.Trim().ToUpperInvariant();

    /// <summary>
    /// Un codice destinatario scritto a mano vince sempre: un cliente estero può averne uno vero
    /// (rappresentante fiscale, sede identificata in Italia) e sovrascriverlo con la convenzione
    /// impedirebbe il recapito. La convenzione interviene solo quando il campo è vuoto.
    /// </summary>
    private static string CodiceDestinatario(DatiCliente? cliente, bool estero) =>
        !string.IsNullOrWhiteSpace(cliente?.CodiceDestinatario)
            ? cliente.CodiceDestinatario.Trim()
            : estero ? CodiceDestinatarioEstero : CodiceDestinatarioItaliano;

    private static string CodiceNatura(NaturaIva natura) => natura switch
    {
        NaturaIva.N1_EscluseArt15 => "N1",
        NaturaIva.N2_1_NonSoggetteArtt7_7Septies => "N2.1",
        NaturaIva.N2_2_NonSoggetteAltriCasi => "N2.2",
        NaturaIva.N3_1_NonImponibiliEsportazioni => "N3.1",
        NaturaIva.N3_2_NonImponibiliCessioniIntracomunitarie => "N3.2",
        NaturaIva.N3_3_NonImponibiliCessioniSanMarino => "N3.3",
        NaturaIva.N3_4_NonImponibiliAssimilateEsportazione => "N3.4",
        NaturaIva.N3_5_NonImponibiliDichiarazioniIntento => "N3.5",
        NaturaIva.N3_6_NonImponibiliAltreNoPlafond => "N3.6",
        NaturaIva.N4_Esenti => "N4",
        NaturaIva.N5_RegimeMargineIvaNonEsposta => "N5",
        NaturaIva.N6_1_ReverseChargeRottamiRecupero => "N6.1",
        NaturaIva.N6_2_ReverseChargeOroArgentoPuro => "N6.2",
        NaturaIva.N6_3_ReverseChargeSubappaltoEdile => "N6.3",
        NaturaIva.N6_4_ReverseChargeCessioneFabbricati => "N6.4",
        NaturaIva.N6_5_ReverseChargeTelefoniCellulari => "N6.5",
        NaturaIva.N6_6_ReverseChargeProdottiElettronici => "N6.6",
        NaturaIva.N6_7_ReverseChargePrestazioniEdiliConnesse => "N6.7",
        NaturaIva.N6_8_ReverseChargeSettoreEnergetico => "N6.8",
        NaturaIva.N6_9_ReverseChargeAltriCasi => "N6.9",
        NaturaIva.N7_IvaAssoltaAltroStatoUE => "N7",
        _ => string.Empty,
    };
}
