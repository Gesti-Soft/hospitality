using System.Globalization;
using System.Xml.Linq;
using GestiSoft.Application.Fatturazione.Sdi;
using GestiSoft.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestiSoft.Infrastructure.Fatturazione;

/// <summary>
/// PDF leggibile di una ricevuta dello SdI, con lo stile del PDF delle fatture. I campi si leggono dal
/// file firmato per nome, come in <see cref="LettoreXmlSdi"/>: i messaggi dello SdI sono di tipi
/// diversi e ognuno ha i suoi campi, quindi si mostra solo quello che c'è.
/// </summary>
public class RicevutaSdiPdfGenerator : IRicevutaSdiPdfGenerator
{
    private const string Inchiostro = "#1B222C";
    private const string InchiostroTenue = "#5B6472";
    private const string Filetto = "#D7DCE3";
    private const string Avviso = "#B7791F";

    public byte[] GeneraPdf(byte[] xml, string? nomeFile, bool prova)
    {
        var radice = LettoreXmlSdi.Radice(xml);
        var letta = LettoreXmlSdi.LeggiRicevuta(xml);
        var titolo = letta is null ? "Messaggio del Sistema di Interscambio" : Titolo(letta.Tipo);
        var voci = radice is null ? [] : Voci(radice, letta).ToList();

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Inchiostro));

                page.Header().Column(testata =>
                {
                    testata.Item().Text("Sistema di Interscambio — Agenzia delle Entrate").FontSize(8.5f).FontColor(InchiostroTenue);
                    testata.Item().PaddingTop(2).Text(titolo).FontSize(19).SemiBold();
                    if (prova)
                    {
                        testata.Item().PaddingTop(4).Text("COLLAUDO — messaggio dello SdI di prova, senza valore fiscale").FontSize(9).SemiBold().FontColor(Avviso);
                    }

                    testata.Item().PaddingTop(12).LineHorizontal(1).LineColor(Filetto);
                });

                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Spacing(16);

                    if (radice is null)
                    {
                        col.Item().Text("Il file ricevuto dallo SdI non è leggibile: scarica il file XML originale.");
                    }

                    if (voci.Count > 0)
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(170);
                                c.RelativeColumn();
                            });
                            foreach (var (etichetta, valore) in voci)
                            {
                                table.Cell().PaddingVertical(4).BorderBottom(0.5f).BorderColor(Filetto).Text(etichetta).FontColor(InchiostroTenue);
                                table.Cell().PaddingVertical(4).BorderBottom(0.5f).BorderColor(Filetto).Text(valore);
                            }
                        });
                    }

                    if (letta is { Errori.Count: > 0 })
                    {
                        col.Item().Text("Errori riscontrati").FontSize(11).SemiBold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(55);
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Element(Testata).Text("Codice");
                                h.Cell().Element(Testata).Text("Descrizione");
                                h.Cell().Element(Testata).Text("Suggerimento");
                            });
                            foreach (var errore in letta.Errori)
                            {
                                table.Cell().Element(Cella).Text(errore.Codice ?? "");
                                table.Cell().Element(Cella).Text(errore.Descrizione ?? "");
                                table.Cell().Element(Cella).Text(errore.Suggerimento ?? "");
                            }
                        });
                    }
                });

                page.Footer().Column(piede =>
                {
                    piede.Item().LineHorizontal(0.5f).LineColor(Filetto);
                    piede.Item().PaddingTop(4).Text(
                        $"Copia leggibile generata dal gestionale dal file firmato dallo SdI{(nomeFile is null ? "" : $" ({nomeFile})")}. "
                        + "Fa fede il file XML originale.").FontSize(7.5f).FontColor(InchiostroTenue);
                });
            });
        });

        return documento.GeneratePdf();
    }

    private static string Titolo(TipoRicevutaSdi tipo) => tipo switch
    {
        TipoRicevutaSdi.Consegna => "Ricevuta di consegna",
        TipoRicevutaSdi.MancataConsegna => "Notifica di impossibilità di recapito",
        TipoRicevutaSdi.Scarto => "Ricevuta di scarto",
        TipoRicevutaSdi.Esito => "Notifica di esito del committente",
        TipoRicevutaSdi.DecorrenzaTermini => "Notifica di decorrenza termini",
        TipoRicevutaSdi.AttestazioneTrasmissione => "Attestazione di avvenuta trasmissione",
        _ => "Messaggio del Sistema di Interscambio",
    };

    /// <summary>I campi del messaggio, nell'ordine in cui servono a chi lo legge. Solo quelli presenti.</summary>
    private static IEnumerable<(string Etichetta, string Valore)> Voci(XElement radice, LettoreXmlSdi.RicevutaLetta? letta)
    {
        string? Campo(string nome) => LettoreXmlSdi.Figlio(radice, nome);

        var destinatario = radice.Elements().FirstOrDefault(e => e.Name.LocalName == "Destinatario");
        var voci = new (string, string?)[]
        {
            ("Identificativo SdI", Campo("IdentificativoSdI")),
            ("File della fattura", Campo("NomeFile")),
            ("Data e ora di ricezione", DataOra(Campo("DataOraRicezione"))),
            ("Data e ora di consegna", DataOra(Campo("DataOraConsegna"))),
            ("Destinatario", destinatario is null
                ? null
                : string.Join(" — ", new[] { LettoreXmlSdi.Figlio(destinatario, "Codice"), LettoreXmlSdi.Figlio(destinatario, "Descrizione") }.Where(t => t is not null))),
            ("Esito", letta?.Descrizione),
            ("Note", letta?.Note),
            ("Impronta del file (hash)", Campo("Hash")),
            ("Identificativo del messaggio", Campo("MessageId")),
        };

        return voci.Where(v => !string.IsNullOrWhiteSpace(v.Item2)).Select(v => (v.Item1, v.Item2!));
    }

    /// <summary>L'ora come la scrive lo SdI (ora italiana con il suo fuso), in formato italiano.</summary>
    private static string? DataOra(string? valore) =>
        DateTimeOffset.TryParse(valore, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)
            : valore;

    private static IContainer Testata(IContainer c) =>
        c.BorderBottom(1).BorderColor(Filetto).PaddingVertical(4).DefaultTextStyle(x => x.FontSize(8.5f).FontColor(InchiostroTenue));

    private static IContainer Cella(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Filetto).PaddingVertical(4);
}
