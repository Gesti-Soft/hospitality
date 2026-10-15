using System.Text;
using GestiSoft.Infrastructure.Fatturazione;

namespace GestiSoft.Api.Tests;

/// <summary>La copia leggibile in PDF delle ricevute dello SdI, sui messaggi d'esempio ufficiali.</summary>
public class RicevutaSdiPdfTests
{
    static RicevutaSdiPdfTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static byte[] Esempio(string tipo) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "MessaggiSdi", $"IT01234567890_11111_{tipo}_001.xml"));

    [Theory]
    [InlineData("RC", false)]
    [InlineData("NS", false)]
    [InlineData("MC", true)]
    [InlineData("DT", false)]
    public void RicevutaUfficiale_ProduceUnPdfValido(string tipo, bool prova)
    {
        var pdf = new RicevutaSdiPdfGenerator().GeneraPdf(Esempio(tipo), $"IT01234567890_11111_{tipo}_001.xml", prova);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    /// <summary>Un file che non è XML non fa fallire il download: il PDF dice di scaricare l'originale.</summary>
    [Fact]
    public void FileIlleggibile_ProduceComunqueUnPdf()
    {
        var pdf = new RicevutaSdiPdfGenerator().GeneraPdf(Encoding.UTF8.GetBytes("non è xml"), null, prova: false);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
