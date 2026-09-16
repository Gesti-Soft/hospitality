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
        var imposta = Math.Round(fattura.ImportoTotale - fattura.PrezzoTotale, 2, MidpointRounding.AwayFromZero);

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
                            c.Item().AlignRight().Text(EtichettaTipoDocumento(fattura.TipoDocumento)).FontSize(11).SemiBold();
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
                        c.Item().Text("Fatturato a").FontSize(8.5f).FontColor(InchiostroTenue);
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
                            header.Cell().Element(CellaTestata).AlignRight().Text("IVA");
                            header.Cell().Element(CellaTestata).AlignRight().Text("Importo");
                        });

                        table.Cell().Element(CellaRiga).Text(fattura.Descrizione ?? "—");
                        table.Cell().Element(CellaRiga).AlignRight().Text(fattura.Quantita.ToString("0.##", Italiano));
                        table.Cell().Element(CellaRiga).AlignRight().Text(Valuta(fattura.PrezzoUnitario));
                        table.Cell().Element(CellaRiga).AlignRight().Text(fattura.AliquotaIva is { } iva ? $"{(int)iva}%" : "—");
                        table.Cell().Element(CellaRiga).AlignRight().Text(Valuta(fattura.PrezzoTotale));
                    });

                    col.Item().AlignRight().Width(230).Column(totali =>
                    {
                        totali.Item().Element(c => RigaTotale(c, "Imponibile", Valuta(fattura.PrezzoTotale), false));
                        totali.Item().Element(c => RigaTotale(c, "Imposta", Valuta(imposta), false));
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

    private static string EtichettaTipoDocumento(TipoDocumentoFattura? tipo) => tipo switch
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

        var cedentePrestatore = new XElement("CedentePrestatore",
            DatiAnagrafici(azienda?.Iso2, azienda?.PIva, azienda?.CodiceFiscale, azienda?.Denominazione, azienda?.Nome, azienda?.Cognome, azienda?.RegimeFiscale),
            Sede(azienda?.Indirizzo, azienda?.NCivico, azienda?.Cap, azienda?.Comune, azienda?.Provincia, azienda?.Nazione));

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

        var datiGenerali = new XElement("DatiGenerali",
            new XElement("DatiGeneraliDocumento",
                new XElement("TipoDocumento", fattura.TipoDocumento is { } td ? $"TD{(int)td:00}" : "TD01"),
                new XElement("Divisa", fattura.Divisa ?? "EUR"),
                new XElement("Data", fattura.DataDocumento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new XElement("Numero", fattura.NumeroDocumento)));

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

        var datiBeniServizi = new XElement("DatiBeniServizi", dettaglioLinee, datiRiepilogo);

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
            // La sede della struttura scrive azienda.Nazione, l'identificativo fiscale usa azienda.Iso2:
            // sono due campi distinti e vanno controllati entrambi.
            motivi.AddRange(ValidaControparte(
                "della struttura", azienda.Nazione, azienda.PIva, azienda.CodiceFiscale,
                azienda.Denominazione, azienda.Nome, azienda.Cognome,
                azienda.Indirizzo, azienda.Comune, azienda.Cap, azienda.Provincia));

            // L'ISO2 della struttura finisce sempre nel file, anche senza partita IVA: identifica
            // chi trasmette.
            if (!NazioneValida(azienda.Iso2))
            {
                motivi.Add("il codice nazione della struttura non è di due lettere (es. IT)");
            }

            if (azienda.RegimeFiscale is null)
            {
                motivi.Add("manca il regime fiscale della struttura");
            }
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
