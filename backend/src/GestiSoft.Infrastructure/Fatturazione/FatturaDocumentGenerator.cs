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
    public byte[] GeneraPdf(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda)
    {
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text($"Fattura n. {fattura.NumeroDocumento} del {fattura.DataDocumento:dd/MM/yyyy}")
                    .FontSize(16).Bold();

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Spacing(15);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c => ScriviAnagrafica(c, "Emittente",
                            azienda?.Denominazione, azienda?.Nome, azienda?.Cognome,
                            azienda?.Indirizzo, azienda?.NCivico, azienda?.Cap, azienda?.Comune, azienda?.Provincia,
                            azienda?.PIva, azienda?.CodiceFiscale));

                        row.RelativeItem().Column(c => ScriviAnagrafica(c, "Destinatario",
                            cliente?.Denominazione, cliente?.Nome, cliente?.Cognome,
                            cliente?.Indirizzo, cliente?.NCivico, cliente?.Cap, cliente?.LuogoResidenza, cliente?.Provincia,
                            cliente?.PIva, cliente?.CodiceFiscale));
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Descrizione").Bold();
                            header.Cell().AlignRight().Text("Qtà").Bold();
                            header.Cell().AlignRight().Text("Prezzo unit.").Bold();
                            header.Cell().AlignRight().Text("IVA").Bold();
                            header.Cell().AlignRight().Text("Totale").Bold();
                        });

                        table.Cell().Text(fattura.Descrizione ?? "-");
                        table.Cell().AlignRight().Text(fattura.Quantita.ToString("0.##", CultureInfo.InvariantCulture));
                        table.Cell().AlignRight().Text(fattura.PrezzoUnitario.ToString("0.00", CultureInfo.InvariantCulture));
                        table.Cell().AlignRight().Text(fattura.AliquotaIva is { } iva ? $"{(int)iva}%" : "-");
                        table.Cell().AlignRight().Text(fattura.ImportoTotale.ToString("0.00", CultureInfo.InvariantCulture));
                    });

                    col.Item().AlignRight().Text($"Totale: {fattura.ImportoTotale:0.00} {fattura.Divisa ?? "EUR"}")
                        .FontSize(14).Bold();
                });

                page.Footer().AlignCenter().Text("Documento generato da GestiSoft Gestionale — non costituisce invio allo SdI")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });

        return documento.GeneratePdf();
    }

    private static void ScriviAnagrafica(
        QuestPDF.Fluent.ColumnDescriptor col, string titolo,
        string? denominazione, string? nome, string? cognome,
        string? indirizzo, string? nCivico, string? cap, string? comune, string? provincia,
        string? pIva, string? codiceFiscale)
    {
        col.Item().Text(titolo).Bold();
        col.Item().Text(!string.IsNullOrWhiteSpace(denominazione) ? denominazione : $"{nome} {cognome}".Trim());
        col.Item().Text($"{indirizzo} {nCivico}".Trim());
        col.Item().Text($"{cap} {comune} ({provincia})".Trim());
        col.Item().Text($"P.IVA: {pIva ?? "-"}   CF: {codiceFiscale ?? "-"}");
    }

    public byte[] GeneraXmlSdi(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda)
    {
        XNamespace p = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        var imponibile = fattura.PrezzoTotale;
        var imposta = Math.Round(fattura.ImportoTotale - fattura.PrezzoTotale, 2, MidpointRounding.AwayFromZero);

        var datiTrasmissione = new XElement("DatiTrasmissione",
            new XElement("IdTrasmittente",
                new XElement("IdPaese", azienda?.Iso2 ?? "IT"),
                new XElement("IdCodice", azienda?.CodiceFiscale ?? azienda?.PIva ?? string.Empty)),
            new XElement("ProgressivoInvio", fattura.Progressivo.ToString("00000", CultureInfo.InvariantCulture)),
            new XElement("FormatoTrasmissione", "FPR12"),
            new XElement("CodiceDestinatario", string.IsNullOrWhiteSpace(cliente?.CodiceDestinatario) ? "0000000" : cliente.CodiceDestinatario));

        var cedentePrestatore = new XElement("CedentePrestatore",
            DatiAnagrafici(azienda?.Iso2, azienda?.PIva, azienda?.CodiceFiscale, azienda?.Denominazione, azienda?.Nome, azienda?.Cognome, azienda?.RegimeFiscale),
            Sede(azienda?.Indirizzo, azienda?.NCivico, azienda?.Cap, azienda?.Comune, azienda?.Provincia, azienda?.Nazione));

        var cessionarioCommittente = new XElement("CessionarioCommittente",
            DatiAnagrafici(cliente?.Iso2, cliente?.PIva, cliente?.CodiceFiscale, cliente?.Denominazione, cliente?.Nome, cliente?.Cognome, regimeFiscale: null),
            Sede(cliente?.Indirizzo, cliente?.NCivico, cliente?.Cap, cliente?.LuogoResidenza, cliente?.Provincia, "IT"));

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
        !string.IsNullOrWhiteSpace(provincia) ? new XElement("Provincia", provincia) : null,
        new XElement("Nazione", string.IsNullOrWhiteSpace(nazione) ? "IT" : nazione));

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
