using System.Globalization;
using GestiSoft.Application.Trattamenti;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestiSoft.Infrastructure.Trattamenti;

/// <summary>
/// Buoni colazione da ritagliare: uno per ospite e per mattina, due per riga. Niente nomi degli
/// ospiti: al bar basta sapere per quale giorno vale e a quale prenotazione appartiene.
/// </summary>
public class BuoniColazioneGenerator : IBuoniColazioneGenerator
{
    /// <summary>Stessi colori del PDF delle fatture.</summary>
    private const string Inchiostro = "#1B222C";
    private const string InchiostroTenue = "#5B6472";
    private const string Filetto = "#D7DCE3";

    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    public byte[] Genera(DatiBuoniColazione dati)
    {
        var buoni = dati.Mattine.SelectMany(giorno => Enumerable.Repeat(giorno, dati.Ospiti)).ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Inchiostro));

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });

                    for (var i = 0; i < buoni.Count; i++)
                    {
                        var numero = i + 1;
                        var giorno = buoni[i];
                        table.Cell().Padding(5).Element(cella => Buono(cella, dati, giorno, numero, buoni.Count));
                    }
                });
            });
        }).GeneratePdf();
    }

    private static void Buono(IContainer cella, DatiBuoniColazione dati, DateTime giorno, int numero, int totale)
    {
        // Bordo tratteggiato non disponibile: un filetto chiaro basta come guida per il taglio.
        cella.Border(1).BorderColor(Filetto).Padding(12).Column(c =>
        {
            c.Item().Text(dati.Titolo).FontSize(13).SemiBold();
            c.Item().PaddingTop(2).Text(dati.EsercizioConvenzionato ?? dati.NomeStruttura).FontSize(10);
            c.Item().PaddingTop(8).Text($"Valido il {giorno.ToString("dddd d MMMM yyyy", Italiano)}").FontSize(10.5f).SemiBold();
            c.Item().PaddingTop(2).Text("Per una persona").FontSize(9).FontColor(InchiostroTenue);

            c.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(Filetto);
            c.Item().PaddingTop(4).Row(r =>
            {
                var riferimento = string.Join(" · ", new[]
                {
                    dati.NumeroPrenotazione is { Length: > 0 } n ? $"Prenotazione {n}" : null,
                    dati.Camera is { Length: > 0 } camera ? camera : null,
                }.Where(v => v is not null));

                r.RelativeItem().Text(riferimento).FontSize(8.5f).FontColor(InchiostroTenue);
                r.AutoItem().Text($"{numero} di {totale}").FontSize(8.5f).FontColor(InchiostroTenue);
            });

            if (dati.EsercizioConvenzionato is not null)
            {
                c.Item().PaddingTop(2).Text($"Offerto da {dati.NomeStruttura}").FontSize(8).FontColor(InchiostroTenue);
            }
        });
    }
}
