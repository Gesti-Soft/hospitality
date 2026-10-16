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
/// Genera PDF e XML SDI di una fattura. Il PDF è un layout nuovo, semplice; l'XML segue la
/// struttura standard "FatturaElettronica ordinaria" (namespace SDI v1.2, FormatoTrasmissione
/// FPR12 verso privati, stessa scelta del legacy) scritta a mano con System.Xml.Linq, e si valida
/// contro lo schema ufficiale con <see cref="ValidaSchema"/> prima di diventare un file da spedire.
/// </summary>
public class FatturaDocumentGenerator : IFatturaDocumentGenerator
{
    /// <summary>Colori presi dai token del frontend, così il documento non sembra di un altro prodotto.</summary>
    private const string Inchiostro = "#1B222C";
    private const string InchiostroTenue = "#5B6472";
    private const string Filetto = "#D7DCE3";

    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    public byte[] GeneraPdf(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda, string? nomeStruttura, DatiSoggiorno? soggiorno = null)
    {
        var intestazione = PrimoNonVuoto(nomeStruttura, azienda?.Denominazione, $"{azienda?.Nome} {azienda?.Cognome}") ?? "Fattura";
        var ricevuta = fattura.TipoEmissione == TipoEmissioneDocumento.Ricevuta;
        var righe = fattura.Righe.OrderBy(r => r.Numero).ToList();
        var riepiloghi = CalcoloFattura.Riepiloghi(righe);
        var imposta = riepiloghi.Sum(r => r.Imposta);
        var nature = righe.Where(r => r.Natura is not null).Select(r => r.Natura!.Value).Distinct().ToList();

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

                    col.Item().Row(riga =>
                    {
                        riga.RelativeItem().Column(c =>
                        {
                            c.Item().Text(fattura.TipoEmissione == TipoEmissioneDocumento.Ricevuta ? "Ricevuta rilasciata a" : "Fatturato a").FontSize(8.5f).FontColor(InchiostroTenue);
                            c.Item().PaddingTop(2).Text(NomeCliente(cliente)).FontSize(11.5f).SemiBold();
                            foreach (var rigaCliente in RigheCliente(cliente))
                            {
                                c.Item().PaddingTop(1).Text(rigaCliente).FontSize(9).FontColor(InchiostroTenue);
                            }
                        });

                        // La ricevuta d'affitto dice quale soggiorno paga: periodo, notti, ospiti e
                        // alloggio, come nei modelli in uso per le locazioni brevi.
                        var vociSoggiorno = ricevuta ? VociSoggiorno(soggiorno, azienda?.IndirizzoImmobile).ToList() : [];
                        if (vociSoggiorno.Count > 0)
                        {
                            riga.ConstantItem(210).Column(c =>
                            {
                                c.Item().Text("Soggiorno").FontSize(8.5f).FontColor(InchiostroTenue);
                                foreach (var (etichetta, valore) in vociSoggiorno)
                                {
                                    c.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.ConstantItem(85).Text(etichetta).FontSize(9).FontColor(InchiostroTenue);
                                        r.RelativeItem().Text(valore).FontSize(9);
                                    });
                                }
                            });
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

                        foreach (var r in righe)
                        {
                            table.Cell().Element(CellaRiga).Text(r.Descrizione);
                            table.Cell().Element(CellaRiga).AlignRight().Text(r.Quantita.ToString("0.##", Italiano));
                            table.Cell().Element(CellaRiga).AlignRight().Text(Valuta(r.PrezzoUnitario));
                            table.Cell().Element(CellaRiga).AlignRight().Text(ricevuta ? "" : r.AliquotaIva is { } iva ? $"{(int)iva}%" : r.Natura is { } n ? CodiceNatura(n) : "—");
                            table.Cell().Element(CellaRiga).AlignRight().Text(Valuta(r.PrezzoTotale));
                        }

                        // Riga a sé anche sulla carta: chi legge deve vedere che quella somma non ha
                        // IVA e perché, non trovarsela confusa nel prezzo del soggiorno.
                        if (fattura.ImpostaSoggiorno is { } tassa and > 0)
                        {
                            table.Cell().Element(CellaRiga).Text("Imposta di soggiorno");
                            table.Cell().Element(CellaRiga).AlignRight().Text("1");
                            table.Cell().Element(CellaRiga).AlignRight().Text(Valuta(tassa));
                            table.Cell().Element(CellaRiga).AlignRight().Text(ricevuta ? "" : "—");
                            table.Cell().Element(CellaRiga).AlignRight().Text(Valuta(tassa));
                        }
                    });

                    col.Item().AlignRight().Width(230).Column(totali =>
                    {
                        totali.Item().Element(c => RigaTotale(c, ricevuta ? "Corrispettivo" : "Imponibile", Valuta(fattura.PrezzoTotale), false));
                        if (!ricevuta)
                        {
                            // Con più aliquote il riepilogo dice quanta IVA per ciascuna, come nel file per lo SdI.
                            var conIva = riepiloghi.Where(r => r.AliquotaIva is not null).ToList();
                            if (conIva.Count > 1)
                            {
                                foreach (var r in conIva)
                                {
                                    totali.Item().Element(c => RigaTotale(c, $"IVA {(int)r.AliquotaIva!.Value}% su {Valuta(r.Imponibile)}", Valuta(r.Imposta), false));
                                }
                            }
                            else
                            {
                                totali.Item().Element(c => RigaTotale(c, "Imposta", Valuta(imposta), false));
                            }
                        }
                        if (fattura.ImpostaSoggiorno is { } tassaTotale and > 0)
                        {
                            totali.Item().Element(c => RigaTotale(c, "Imposta di soggiorno", Valuta(tassaTotale), false));
                        }

                        totali.Item().PaddingTop(5).BorderTop(1).BorderColor(Filetto).PaddingTop(6)
                            .Element(c => RigaTotale(c, "Totale", Valuta(fattura.ImportoTotale), true));

                        if (ricevuta && fattura.ModalitaPagamento is { } pagamento)
                        {
                            totali.Item().PaddingTop(6).Element(c => RigaTotale(c, "Pagato con", EtichettaPagamento(pagamento), false));
                        }
                    });

                    // La dicitura di legge vince sempre sul codice: il codice natura dice allo SdI
                    // perché l'IVA non c'è, la dicitura lo dice a chi legge la fattura, ed è quella
                    // che il fisco pretende sul documento.
                    if (!string.IsNullOrWhiteSpace(azienda?.DicituraFattura))
                    {
                        col.Item().Text(azienda.DicituraFattura!.Trim()).FontSize(8.5f).FontColor(InchiostroTenue);
                    }
                    else if (!ricevuta && nature.Count > 0)
                    {
                        col.Item().Text(righe.All(r => r.Natura is not null)
                            ? $"Operazione non soggetta a IVA — natura {string.Join(", ", nature.Select(CodiceNatura))}."
                            : $"Righe senza IVA — natura {string.Join(", ", nature.Select(CodiceNatura))}.").FontSize(8.5f).FontColor(InchiostroTenue);
                    }

                    if (ricevuta)
                    {
                        col.Item().Text("Locazione breve — operazione fuori dal campo di applicazione dell'IVA. Documento non fiscale.").FontSize(8.5f).FontColor(InchiostroTenue);

                        if (azienda?.CedolareSecca == true)
                        {
                            col.Item().Text("Il locatore ha optato per il regime della cedolare secca (art. 3 D.Lgs. 23/2011; art. 4 D.L. 50/2017).").FontSize(8.5f).FontColor(InchiostroTenue);
                        }
                    }

                    if (fattura.ImpostaSoggiorno is > 0)
                    {
                        col.Item().Text(ricevuta
                            // Sulla ricevuta non c'è IVA da cui escluderla: quello che conta per l'ospite è
                            // che quella somma va al Comune, non al locatore.
                            ? "Imposta di soggiorno riscossa per conto del Comune: non fa parte del corrispettivo della locazione."
                            : "Imposta di soggiorno esclusa dalla base imponibile ai sensi dell'art. 15 c.1 n.3 DPR 633/72.").FontSize(8.5f).FontColor(InchiostroTenue);
                    }

                    // Sulla ricevuta di un privato il bollo è la marca di carta da incollare
                    // sull'originale: la formula del bollo virtuale vale per la fattura elettronica
                    // (o per chi ha l'autorizzazione dell'Agenzia), lì sarebbe falsa. Il riquadro
                    // ricorda all'host che la marca serve e dove va.
                    if (fattura.ImportoBollo is > 0 && ricevuta)
                    {
                        col.Item().PaddingTop(10).Width(95).Height(95).Border(1).BorderColor(Filetto)
                            .AlignCenter().AlignMiddle().PaddingHorizontal(6)
                            .Text($"Marca da bollo\nda {Valuta(fattura.ImportoBollo.Value)}").FontSize(8).FontColor(InchiostroTenue).AlignCenter();
                    }
                    // Va stampata sul documento, non basta il blocco DatiBollo nell'XML.
                    else if (fattura.ImportoBollo is > 0)
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

    private static IEnumerable<(string Etichetta, string Valore)> VociSoggiorno(DatiSoggiorno? s, string? indirizzoImmobile)
    {
        // L'immobile si stampa anche senza prenotazione collegata: dice dove si è soggiornato.
        if (!string.IsNullOrWhiteSpace(indirizzoImmobile)) yield return ("Immobile", indirizzoImmobile.Trim());

        if (s is null) yield break;

        if (s.Arrivo is { } arrivo) yield return ("Arrivo", arrivo.ToString("d MMMM yyyy", Italiano));
        if (s.Partenza is { } partenza) yield return ("Partenza", partenza.ToString("d MMMM yyyy", Italiano));
        if (s.Notti is { } notti) yield return ("Notti", notti.ToString(Italiano));
        if (s.NumeroOspiti is { } ospiti and > 0) yield return ("Ospiti", ospiti.ToString(Italiano));
        if (!string.IsNullOrWhiteSpace(s.Alloggio)) yield return ("Alloggio", s.Alloggio.Trim());
        if (!string.IsNullOrWhiteSpace(s.NumeroPrenotazione)) yield return ("Prenotazione", s.NumeroPrenotazione.Trim());
    }

    private static string EtichettaPagamento(ModalitaPagamento m) => m switch
    {
        ModalitaPagamento.Contanti => "Contanti",
        ModalitaPagamento.Bonifico => "Bonifico",
        ModalitaPagamento.CartaDiPagamento => "Carta di pagamento",
        ModalitaPagamento.Assegno => "Assegno",
        ModalitaPagamento.PortaleOnline => "Portale di prenotazione",
        _ => "—",
    };

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

    public IReadOnlyList<string> ValidaSchema(byte[] xml) => SchemaFatturaPA.Valida(xml);

    public byte[] GeneraXmlSdi(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda, IntestazioneTrasmissione? trasmissione = null, DatiFattura? stornata = null)
    {
        XNamespace p = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        var righe = fattura.Righe.OrderBy(r => r.Numero).ToList();
        var clienteEstero = Estero(cliente?.Iso2);

        trasmissione ??= new IntestazioneTrasmissione(
            CodiceNazione(azienda?.Iso2),
            azienda?.CodiceFiscale ?? azienda?.PIva ?? string.Empty,
            fattura.Progressivo.ToString("00000", CultureInfo.InvariantCulture));

        // La PEC serve solo quando non c'è un codice destinatario vero: con un codice, lo SdI recapita lì.
        var codiceDestinatario = CodiceDestinatario(cliente, clienteEstero);
        var pec = codiceDestinatario == CodiceDestinatarioItaliano && PecValida(cliente?.Pec) ? cliente!.Pec!.Trim() : null;

        var datiTrasmissione = new XElement("DatiTrasmissione",
            new XElement("IdTrasmittente",
                new XElement("IdPaese", trasmissione.IdPaese),
                new XElement("IdCodice", trasmissione.IdCodice)),
            new XElement("ProgressivoInvio", trasmissione.ProgressivoInvio),
            new XElement("FormatoTrasmissione", "FPR12"),
            new XElement("CodiceDestinatario", codiceDestinatario),
            pec is null ? null : new XElement("PECDestinatario", pec));

        // La nazione della sede è l'ISO2, non il campo "Nazione" dei dati aziendali: quello è una
        // denominazione leggibile ("Italia") e nel tracciato vale solo il codice a due lettere. Ci
        // finiva davvero, e bastava a impedire la generazione del file.
        var cedentePrestatore = new XElement("CedentePrestatore",
            DatiAnagrafici(azienda?.Iso2, azienda?.PIva, azienda?.CodiceFiscale, azienda?.Denominazione, azienda?.Nome, azienda?.Cognome, azienda?.RegimeFiscale, estero: false),
            Sede(azienda?.Indirizzo, azienda?.NCivico, azienda?.Cap, azienda?.Comune, azienda?.Provincia, azienda?.Iso2));

        // Per un cessionario non residente l'indirizzo segue le convenzioni dello SDI: CAP fisso a
        // "00000" (il codice postale vero, se serve, va scritto dentro l'Indirizzo) e Provincia
        // omessa, che è valorizzabile solo per l'Italia. La Nazione è quella del cliente: prima era
        // scritta fissa a "IT" e un cliente estero risultava residente in Italia.
        var cessionarioCommittente = new XElement("CessionarioCommittente",
            DatiAnagrafici(cliente?.Iso2, cliente?.PIva, cliente?.CodiceFiscale, cliente?.Denominazione, cliente?.Nome, cliente?.Cognome, regimeFiscale: null, clienteEstero),
            Sede(
                cliente?.Indirizzo,
                cliente?.NCivico,
                clienteEstero ? CapEstero : cliente?.Cap,
                cliente?.LuogoResidenza,
                clienteEstero ? null : cliente?.Provincia,
                cliente?.Iso2));

        // L'ordine degli elementi dentro DatiGeneraliDocumento è fissato dallo schema: TipoDocumento,
        // Divisa, Data, Numero, DatiBollo, ImportoTotaleDocumento, ... — invertirli fa scartare il file.
        // Il totale comprende IVA e imposta di soggiorno (è una riga del documento), non il bollo: il
        // blocco DatiBollo lo dichiara, non lo addebita.
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
                    : null,
                new XElement("ImportoTotaleDocumento", fattura.ImportoTotale.ToString("0.00", CultureInfo.InvariantCulture))),
            // La nota di credito dice quale fattura storna: numero e data dell'originale.
            stornata is null
                ? null
                : new XElement("DatiFattureCollegate",
                    new XElement("IdDocumento", stornata.NumeroDocumento),
                    new XElement("Data", stornata.DataDocumento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));

        // Una riga per voce, ognuna con la sua aliquota: AliquotaIVA è obbligatoria, e dove l'IVA non c'è
        // vale 0.00 accanto alla natura. Il formato vuole due decimali ("10.00"): "10" non passa.
        var dettaglioLinee = righe.Select((r, i) => new XElement("DettaglioLinee",
            new XElement("NumeroLinea", i + 1),
            new XElement("Descrizione", TestoFatturaPA.Pulisci(r.Descrizione, TestoFatturaPA.Descrizione) ?? "-"),
            new XElement("Quantita", r.Quantita.ToString("0.00", CultureInfo.InvariantCulture)),
            new XElement("PrezzoUnitario", r.PrezzoUnitario.ToString("0.00000", CultureInfo.InvariantCulture)),
            new XElement("PrezzoTotale", r.PrezzoTotale.ToString("0.00", CultureInfo.InvariantCulture)),
            new XElement("AliquotaIVA", Aliquota(r.AliquotaIva)),
            r.Natura is { } natura ? new XElement("Natura", CodiceNatura(natura)) : null)).ToList();

        // Un riepilogo per aliquota (o natura), con l'IVA calcolata sul totale del gruppo: la stessa
        // regola di CalcoloFattura, che dà il totale salvato sulla fattura.
        var datiRiepilogo = CalcoloFattura.Riepiloghi(righe).Select(r => new XElement("DatiRiepilogo",
            new XElement("AliquotaIVA", Aliquota(r.AliquotaIva)),
            r.Natura is { } natura ? new XElement("Natura", CodiceNatura(natura)) : null,
            new XElement("ImponibileImporto", r.Imponibile.ToString("0.00", CultureInfo.InvariantCulture)),
            new XElement("Imposta", r.Imposta.ToString("0.00", CultureInfo.InvariantCulture)),
            new XElement("EsigibilitaIVA", "I"))).ToList();

        // L'imposta di soggiorno è una somma anticipata in nome e per conto del cliente verso il
        // Comune: esclusa dalla base imponibile ex art. 15 c.1 n.3 DPR 633/72, quindi riga a sé con
        // natura N1 e riepilogo separato, mai sommata all'imponibile del soggiorno — lì dentro
        // pagherebbe un'IVA che non deve.
        var rigaImpostaSoggiorno = fattura.ImpostaSoggiorno is { } impostaSoggiorno and > 0
            ? new XElement("DettaglioLinee",
                new XElement("NumeroLinea", righe.Count + 1),
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
                new XElement("EsigibilitaIVA", "I"),
                new XElement("RiferimentoNormativo", RiferimentoImpostaSoggiorno))
            : null;

        var datiBeniServizi = new XElement("DatiBeniServizi", dettaglioLinee, rigaImpostaSoggiorno, datiRiepilogo, riepilogoImpostaSoggiorno);

        // Come ha pagato il cliente, se l'operatore l'ha indicato: pagamento completo (TP02) dell'intero
        // totale. Una nota di credito non ha un pagamento da dichiarare.
        var datiPagamento = fattura.ModalitaPagamento is { } modalita && fattura.TipoDocumento != TipoDocumentoFattura.TD04_NotaDiCredito
            ? new XElement("DatiPagamento",
                new XElement("CondizioniPagamento", "TP02"),
                new XElement("DettaglioPagamento",
                    new XElement("ModalitaPagamento", CodiceModalitaPagamento(modalita)),
                    new XElement("ImportoPagamento", fattura.ImportoTotale.ToString("0.00", CultureInfo.InvariantCulture))))
            : null;

        var header = new XElement("FatturaElettronicaHeader", datiTrasmissione, cedentePrestatore, cessionarioCommittente);
        var body = new XElement("FatturaElettronicaBody", datiGenerali, datiBeniServizi, datiPagamento);

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
    public IReadOnlyList<string> ValidaPerSdi(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda, DatiFattura? stornata = null)
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

            // Chi emette una fattura ha sempre la partita IVA: il tracciato la vuole obbligatoria.
            if (string.IsNullOrWhiteSpace(azienda.PIva))
            {
                motivi.Add("manca la partita IVA della struttura");
            }
        }

        if (fattura.FatturaStornataId is not null && stornata is null)
        {
            motivi.Add("la nota di credito non trova la fattura che storna");
        }

        // Descrizione di ogni riga: obbligatoria nel tracciato. Una fattura creata altrove, o di prima
        // delle righe, poteva uscire con l'elemento vuoto e lo SDI la scartava giorni dopo.
        if (fattura.Righe.Count == 0)
        {
            motivi.Add("la fattura non ha righe");
        }
        else if (fattura.Righe.Any(r => string.IsNullOrWhiteSpace(r.Descrizione)))
        {
            motivi.Add("manca la descrizione di una riga della fattura");
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

            // Sette caratteri, lettere maiuscole e cifre: un codice sbagliato manda la fattura su un
            // altro canale, o la fa scartare. Quello a sei è della Pubblica Amministrazione, fuori ambito.
            if (!string.IsNullOrWhiteSpace(cliente.CodiceDestinatario))
            {
                var codice = cliente.CodiceDestinatario.Trim().ToUpperInvariant();
                if (codice.Length != 7 || !codice.All(char.IsAsciiLetterOrDigit))
                {
                    motivi.Add(codice.Length == 6
                        ? "il codice destinatario del cliente ha sei caratteri, cioè è di una Pubblica Amministrazione: le fatture verso la PA non sono gestite"
                        : "il codice destinatario del cliente non è valido: servono sette caratteri, lettere e cifre");
                }
            }

            if (!string.IsNullOrWhiteSpace(cliente.Pec) && !PecValida(cliente.Pec))
            {
                motivi.Add("la PEC del cliente non è un indirizzo email valido");
            }
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

        // Uno straniero spesso non ha né partita IVA né un codice fiscale italiano: il file porta allora
        // un identificativo convenzionale (vedi DatiAnagrafici), e la fattura si emette lo stesso.
        if (!estero && string.IsNullOrWhiteSpace(pIva) && string.IsNullOrWhiteSpace(codiceFiscale))
        {
            motivi.Add($"manca la partita IVA o il codice fiscale {chi}");
        }

        // Solo la forma e il carattere di controllo: se esistono davvero lo sa solo lo SdI (scarti
        // 00305/00306). Basta però a fermare prima dell'invio una cifra sbagliata o due invertite.
        if (!estero && Codice(pIva) is { } partitaIva && !PartitaIvaValida(partitaIva is { Length: 13 } && partitaIva.StartsWith("IT", StringComparison.Ordinal) ? partitaIva[2..] : partitaIva))
        {
            motivi.Add($"la partita IVA {chi} non è valida: controlla le cifre");
        }

        if (!estero && Codice(codiceFiscale) is { } cf && !(cf.Length == 11 ? PartitaIvaValida(cf) : CodiceFiscaleValido(cf)))
        {
            motivi.Add($"il codice fiscale {chi} non è valido: controlla i caratteri");
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

    /// <summary>
    /// Partita IVA italiana (o codice fiscale numerico di una società): 11 cifre, l'ultima di controllo.
    /// Le cifre in posizione pari si raddoppiano (meno 9 se superano 9), si somma tutto e il totale
    /// deve essere un multiplo di 10.
    /// </summary>
    internal static bool PartitaIvaValida(string partitaIva)
    {
        if (partitaIva.Length != 11 || !partitaIva.All(char.IsAsciiDigit))
        {
            return false;
        }

        var somma = 0;
        for (var i = 0; i < 11; i++)
        {
            var cifra = partitaIva[i] - '0';
            if (i % 2 == 1)
            {
                cifra *= 2;
                if (cifra > 9)
                {
                    cifra -= 9;
                }
            }

            somma += cifra;
        }

        return somma % 10 == 0;
    }

    /// <summary>
    /// Codice fiscale di persona fisica: 16 caratteri, l'ultimo di controllo (DM 23/12/1976). Ammette
    /// l'omocodia (lettere al posto delle cifre): le tabelle di conversione valgono uguali per le due.
    /// </summary>
    internal static bool CodiceFiscaleValido(string codiceFiscale)
    {
        if (codiceFiscale.Length != 16 || !System.Text.RegularExpressions.Regex.IsMatch(
                codiceFiscale, "^[A-Z]{6}[0-9LMNPQRSTUV]{2}[ABCDEHLMPRST][0-9LMNPQRSTUV]{2}[A-Z][0-9LMNPQRSTUV]{3}[A-Z]$"))
        {
            return false;
        }

        // Valori dei caratteri in posizione dispari (1ª, 3ª, ...), per cifre 0-9 e lettere A-Z.
        int[] dispari = [1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18, 20, 11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23];
        var somma = 0;
        for (var i = 0; i < 15; i++)
        {
            var c = codiceFiscale[i];
            var indice = char.IsAsciiDigit(c) ? c - '0' : c - 'A';
            somma += i % 2 == 0 ? dispari[indice] : indice;
        }

        return codiceFiscale[15] == (char)('A' + somma % 26);
    }

    private static bool CapValido(string? cap) =>
        !string.IsNullOrWhiteSpace(cap) && cap.Trim().Length == 5 && cap.Trim().All(char.IsAsciiDigit);

    private static bool ProvinciaValida(string? provincia) =>
        provincia is not null && provincia.Trim().Length == 2 && provincia.Trim().All(char.IsLetter);

    /// <param name="estero">
    /// Controparte non residente: l'identificativo va sempre in IdFiscaleIVA col paese vero, e mai in
    /// CodiceFiscale (che ammette solo il formato italiano). Per un privato straniero senza codice si
    /// scrive un identificativo convenzionale: lo SdI non lo controlla per i soggetti esteri, e senza
    /// nessun identificativo il file viene scartato.
    /// </param>
    private static XElement DatiAnagrafici(string? iso2, string? pIva, string? codiceFiscale, string? denominazione, string? nome, string? cognome, RegimeFiscale? regimeFiscale, bool estero)
    {
        // Denominazione oppure nome e cognome, mai entrambi (scarto 00200).
        var anagrafica = TestoFatturaPA.Pulisci(denominazione, TestoFatturaPA.Denominazione) is { } ragioneSociale
            ? new XElement("Anagrafica", new XElement("Denominazione", ragioneSociale))
            : new XElement("Anagrafica",
                new XElement("Nome", TestoFatturaPA.Pulisci(nome, TestoFatturaPA.NomeCognome) ?? string.Empty),
                new XElement("Cognome", TestoFatturaPA.Pulisci(cognome, TestoFatturaPA.NomeCognome) ?? string.Empty));

        var partitaIva = Codice(pIva);
        var codice = Codice(codiceFiscale);
        var paese = CodiceNazione(iso2);

        XElement? idFiscale;
        XElement? cf;
        if (estero)
        {
            var identificativo = partitaIva ?? codice ?? IdentificativoEsteroConvenzionale;
            // Una partita IVA UE si scrive spesso col prefisso del paese ("DE123..."): IdPaese lo dice già.
            if (identificativo.Length > 2 && identificativo.StartsWith(paese, StringComparison.Ordinal) && identificativo != IdentificativoEsteroConvenzionale)
            {
                identificativo = identificativo[2..];
            }

            idFiscale = new XElement("IdFiscaleIVA", new XElement("IdPaese", paese), new XElement("IdCodice", identificativo.Length > 28 ? identificativo[..28] : identificativo));
            cf = null;
        }
        else
        {
            if (partitaIva is { Length: 13 } && partitaIva.StartsWith("IT", StringComparison.Ordinal))
            {
                partitaIva = partitaIva[2..];
            }

            idFiscale = partitaIva is null ? null : new XElement("IdFiscaleIVA", new XElement("IdPaese", paese), new XElement("IdCodice", partitaIva));
            cf = codice is null ? null : new XElement("CodiceFiscale", codice);
        }

        return new XElement("DatiAnagrafici",
            idFiscale,
            cf,
            anagrafica,
            regimeFiscale is { } regime ? new XElement("RegimeFiscale", $"RF{(int)regime:00}") : null);
    }

    /// <summary>Un codice fiscale o una partita IVA scritti a mano: maiuscolo, senza spazi, punti o trattini.</summary>
    private static string? Codice(string? valore)
    {
        var pulito = new string((valore ?? "").Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        return pulito.Length == 0 ? null : pulito;
    }

    private static XElement Sede(string? indirizzo, string? nCivico, string? cap, string? comune, string? provincia, string? nazione) => new(
        "Sede",
        new XElement("Indirizzo", TestoFatturaPA.Pulisci(indirizzo, TestoFatturaPA.Indirizzo) ?? string.Empty),
        // NumeroCivico ammette solo Basic Latin: un civico con lettere accentate si scrive dentro l'indirizzo.
        TestoFatturaPA.Pulisci(nCivico, TestoFatturaPA.NumeroCivico) is { } civico && civico.All(c => c < '\u0080')
            ? new XElement("NumeroCivico", civico)
            : null,
        new XElement("CAP", string.IsNullOrWhiteSpace(cap) ? "00000" : cap.Trim()),
        new XElement("Comune", TestoFatturaPA.Pulisci(comune, TestoFatturaPA.Comune) ?? string.Empty),
        !string.IsNullOrWhiteSpace(provincia) ? new XElement("Provincia", provincia.Trim().ToUpperInvariant()) : null,
        new XElement("Nazione", CodiceNazione(nazione)));

    /// <summary>Identificativo di un cessionario estero che non ne ha uno (privato straniero): convenzione diffusa, lo SdI non lo verifica.</summary>
    private const string IdentificativoEsteroConvenzionale = "99999999999";

    /// <summary>Dicitura di legge sul riepilogo dell'imposta di soggiorno (massimo 100 caratteri nel tracciato).</summary>
    private const string RiferimentoImpostaSoggiorno = "Escluse ex art. 15 c.1 n.3 DPR 633/72";

    /// <summary>Un indirizzo email ben formato: una PEC sbagliata fa scartare il file dallo schema.</summary>
    private static bool PecValida(string? pec) =>
        !string.IsNullOrWhiteSpace(pec)
        && pec.Trim().Length is >= 7 and <= 256
        && System.Text.RegularExpressions.Regex.IsMatch(pec.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    /// <summary>
    /// Codici MP del tracciato. L'incasso tramite il portale di prenotazione è un pagamento elettronico
    /// con carta (MP08): è così che il portale lo incassa dall'ospite.
    /// </summary>
    private static string CodiceModalitaPagamento(ModalitaPagamento modalita) => modalita switch
    {
        ModalitaPagamento.Contanti => "MP01",
        ModalitaPagamento.Assegno => "MP02",
        ModalitaPagamento.Bonifico => "MP05",
        ModalitaPagamento.CartaDiPagamento => "MP08",
        ModalitaPagamento.PortaleOnline => "MP08",
        _ => "MP01",
    };

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
            ? cliente.CodiceDestinatario.Trim().ToUpperInvariant()
            : estero ? CodiceDestinatarioEstero : CodiceDestinatarioItaliano;

    /// <summary>Aliquota come la vuole il tracciato: due decimali, 0.00 dove l'IVA non c'è.</summary>
    private static string Aliquota(AliquotaIva? aliquota) =>
        (aliquota is { } a ? (int)a : 0).ToString("0.00", CultureInfo.InvariantCulture);

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
