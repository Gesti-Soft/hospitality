using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Totali di una fattura a righe: IVA calcolata sul totale di ogni aliquota (come il riepilogo dello
/// SdI), imposta di soggiorno fuori dall'imponibile, righe con natura senza IVA.
/// </summary>
public class CalcoloFatturaTests
{
    private static RigaFattura Riga(decimal totale, AliquotaIva? aliquota, NaturaIva? natura = null) =>
        new() { PrezzoTotale = totale, AliquotaIva = aliquota, Natura = natura };

    [Fact]
    public void AlloggioESpa_TotaleConDueAliquote()
    {
        var righe = new List<RigaFattura> { Riga(200m, AliquotaIva.Iva10), Riga(90m, AliquotaIva.Iva22) };

        Assert.Equal(290m, CalcoloFattura.Imponibile(righe));
        Assert.Equal(39.80m, CalcoloFattura.Imposta(righe));
        Assert.Equal(341.80m, CalcoloFattura.Totale(righe, impostaSoggiorno: 12m));
    }

    /// <summary>Tre righe da 0,15 € al 22%: riga per riga l'IVA sarebbe 0,03 × 3 = 0,09; sul totale di 0,45 è 0,10, come vuole lo SdI.</summary>
    [Fact]
    public void IvaSulTotaleDellAliquota_NonRigaPerRiga()
    {
        var righe = new List<RigaFattura> { Riga(0.15m, AliquotaIva.Iva22), Riga(0.15m, AliquotaIva.Iva22), Riga(0.15m, AliquotaIva.Iva22) };

        var riepilogo = Assert.Single(CalcoloFattura.Riepiloghi(righe));
        Assert.Equal(0.45m, riepilogo.Imponibile);
        Assert.Equal(0.10m, riepilogo.Imposta);
    }

    [Fact]
    public void RigaConNatura_NonHaIva()
    {
        var righe = new List<RigaFattura> { Riga(200m, null, NaturaIva.N2_2_NonSoggetteAltriCasi) };

        Assert.Equal(0m, CalcoloFattura.Imposta(righe));
        Assert.Equal(200m, CalcoloFattura.Totale(righe, 0m));
    }

    [Fact]
    public void AliquotaENatura_ConvenzioneZeroPiuNatura()
    {
        Assert.Equal((AliquotaIva.Iva10, null), CalcoloFattura.AliquotaENatura(AliquotaIva.Iva10, null, "Riga 1"));
        Assert.Equal((AliquotaIva.Iva0, NaturaIva.N2_2_NonSoggetteAltriCasi), CalcoloFattura.AliquotaENatura(AliquotaIva.Iva0, NaturaIva.N2_2_NonSoggetteAltriCasi, "Riga 1"));
        // Solo la natura: si salva comunque come 0% più natura.
        Assert.Equal((AliquotaIva.Iva0, NaturaIva.N4_Esenti), CalcoloFattura.AliquotaENatura(null, NaturaIva.N4_Esenti, "Riga 1"));
    }

    [Fact]
    public void AliquotaENatura_CombinazioniSbagliate_Rifiutate()
    {
        Assert.Throws<Exceptions.ConflictException>(() => CalcoloFattura.AliquotaENatura(AliquotaIva.Iva22, NaturaIva.N4_Esenti, "Riga 1"));
        Assert.Throws<Exceptions.ConflictException>(() => CalcoloFattura.AliquotaENatura(AliquotaIva.Iva0, null, "Riga 1"));
        Assert.Throws<Exceptions.ConflictException>(() => CalcoloFattura.AliquotaENatura(null, null, "Riga 1"));
    }

    [Fact]
    public void TotaleRiga_ArrotondatoAlCentesimo()
    {
        Assert.Equal(33.34m, CalcoloFattura.TotaleRiga(3m, 11.113m));
    }

    [Fact]
    public void Riepiloghi_UnoPerAliquotaENatura()
    {
        var righe = new List<RigaFattura>
        {
            Riga(100m, AliquotaIva.Iva10),
            Riga(50m, AliquotaIva.Iva10),
            Riga(40m, AliquotaIva.Iva22),
            Riga(30m, null, NaturaIva.N4_Esenti),
        };

        var riepiloghi = CalcoloFattura.Riepiloghi(righe);

        Assert.Equal(3, riepiloghi.Count);
        Assert.Equal(150m, riepiloghi.Single(r => r.AliquotaIva == AliquotaIva.Iva10).Imponibile);
    }
}
