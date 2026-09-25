using System.Text;
using GestiSoft.Application.Trattamenti;
using GestiSoft.Infrastructure.Trattamenti;

namespace GestiSoft.Api.Tests;

/// <summary>I buoni colazione si generano: uno per ospite e per mattina, dati finti.</summary>
public class BuoniColazioneTests
{
    /// <summary>La licenza QuestPDF la imposta Program.cs all'avvio dell'Api; qui il generatore è usato da solo.</summary>
    static BuoniColazioneTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    [Fact]
    public void GeneraUnPdf()
    {
        var mattine = new[] { new DateTime(2026, 10, 23), new DateTime(2026, 10, 24), new DateTime(2026, 10, 25) };
        var pdf = new BuoniColazioneGenerator().Genera(new DatiBuoniColazione("Villa di prova", "Bar Centrale", "125", "Camera 3", 2, mattine));

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        // Copia da guardare a mano, fuori dal repository.
        if (Environment.GetEnvironmentVariable("GESTISOFT_PDF_DI_PROVA") is { Length: > 0 } cartella)
        {
            File.WriteAllBytes(Path.Combine(cartella, "buoni-colazione-prova.pdf"), pdf);
        }
    }
}
