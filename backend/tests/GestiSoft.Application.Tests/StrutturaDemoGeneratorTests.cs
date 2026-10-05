using GestiSoft.Application.SuperAdmin;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

public class StrutturaDemoGeneratorTests
{
    private static readonly DateTime Oggi = new(2026, 10, 5);

    [Theory]
    [InlineData(1)]
    [InlineData(20260924)]
    [InlineData(987654)]
    public void Genera_venti_camere_senza_soggiorni_sovrapposti(int seme)
    {
        var dati = StrutturaDemoGenerator.Genera(Oggi, seme);

        Assert.Equal(20, dati.Camere.Count);
        Assert.Equal(20, dati.Camere.Select(c => c.Nome).Distinct().Count());
        Assert.All(dati.Prenotazioni, p => Assert.True(p.CheckOut > p.CheckIn));

        foreach (var perCamera in dati.Prenotazioni.GroupBy(p => p.CameraId))
        {
            var ordinate = perCamera.OrderBy(p => p.CheckIn).ToList();
            for (var i = 1; i < ordinate.Count; i++)
            {
                Assert.True(ordinate[i].CheckIn >= ordinate[i - 1].CheckOut, "Due soggiorni nella stessa camera si sovrappongono.");
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20260924)]
    public void Importo_pagato_uguale_al_registro_e_nulla_nel_futuro(int seme)
    {
        var dati = StrutturaDemoGenerator.Genera(Oggi, seme);
        var pagatoPerPrenotazione = dati.Pagamenti.ToLookup(p => p.PrenotazioneId);

        Assert.All(dati.Prenotazioni, p =>
        {
            Assert.Equal(p.ImportoPagato, pagatoPerPrenotazione[p.Id].Sum(x => x.Importo));
            Assert.True(p.CreatedAtUtc.Date <= Oggi, "Prenotazione presa in una data futura.");
        });
        Assert.All(dati.Pagamenti, p => Assert.True(p.Data <= DateOnly.FromDateTime(Oggi), "Pagamento registrato in una data futura."));
        Assert.All(dati.Entrate, e => Assert.True(e.Data <= Oggi));
        Assert.All(dati.Spese, s => Assert.True(s.DataSpesa <= Oggi));
    }

    [Fact]
    public void La_giornata_di_oggi_ha_ospiti_in_casa_e_stati_coerenti()
    {
        var dati = StrutturaDemoGenerator.Genera(Oggi, 20260924);
        var oggiUtc = DateTime.SpecifyKind(Oggi, DateTimeKind.Utc);

        var inCasa = dati.Prenotazioni.Where(p => p.StatoPrenotazione == StatoPrenotazione.InCorso).ToList();
        Assert.NotEmpty(inCasa);
        Assert.All(inCasa, p => Assert.True(p.CheckIn <= oggiUtc && p.CheckOut >= oggiUtc));
        Assert.All(inCasa, p => Assert.Equal(StatoCamera.Occupata, dati.Camere.Single(c => c.Id == p.CameraId).StateRoom));

        Assert.All(
            dati.Prenotazioni.Where(p => p.CheckOut < oggiUtc && p.StatoPrenotazione != StatoPrenotazione.Annullata),
            p => Assert.Equal(StatoPrenotazione.Completata, p.StatoPrenotazione));
        Assert.All(
            dati.Prenotazioni.Where(p => p.CheckIn > oggiUtc && p.StatoPrenotazione != StatoPrenotazione.Annullata),
            p => Assert.Equal(StatoPrenotazione.Incompleta, p.StatoPrenotazione));
    }

    [Fact]
    public void Ospiti_coerenti_con_la_prenotazione_e_nessuna_integrazione_attiva()
    {
        var dati = StrutturaDemoGenerator.Genera(Oggi, 20260924);
        var righePerOspite = dati.OspitiRighe.ToLookup(r => r.OspiteId);

        Assert.Equal(dati.Prenotazioni.Count, dati.Ospiti.Count);
        foreach (var p in dati.Prenotazioni)
        {
            var ospite = dati.Ospiti.Single(o => o.PrenotazioneId == p.Id);
            Assert.Equal(p.NumeroOspiti, 1 + righePerOspite[ospite.Id].Count());
            Assert.Equal(p.EtaBambini.Count, righePerOspite[ospite.Id].Count(r => r.EsenteDaTassa));
            Assert.Null(ospite.NumeroDocumento);
        }

        Assert.False(dati.Struttura.WubookAbilitato || dati.Struttura.AlloggiatiWebAbilitato || dati.Struttura.OsservatorioAbilitato || dati.Struttura.PayTouristAbilitato);
        Assert.False(dati.Impostazioni.PoliziaStatoAttiva || dati.Impostazioni.OsservatorioAttivo || dati.Impostazioni.PayTouristAttivo);
        Assert.Equal(4, dati.Trattamenti.Count);
    }

    [Fact]
    public void Assegnata_a_un_cliente_esistente_non_ne_crea_uno_nuovo()
    {
        var clienteId = Guid.NewGuid();

        var assegnata = StrutturaDemoGenerator.Genera(Oggi, 1, clienteId);
        var conClienteNuovo = StrutturaDemoGenerator.Genera(Oggi, 1);

        Assert.Null(assegnata.Cliente);
        Assert.Equal(clienteId, assegnata.Struttura.ClienteId);
        Assert.NotNull(conClienteNuovo.Cliente);
        Assert.Equal(conClienteNuovo.Cliente.Id, conClienteNuovo.Struttura.ClienteId);
        Assert.True(assegnata.Struttura.Demo && conClienteNuovo.Struttura.Demo);
    }

    [Fact]
    public void Il_cliente_demo_nuovo_prende_il_nome_scelto()
    {
        var dati = StrutturaDemoGenerator.Genera(Oggi, 1, clienteId: null, nomeClienteNuovo: "  Hotel Rossi demo ");

        Assert.Equal("Hotel Rossi demo", dati.Cliente!.RagioneSociale);
    }

    [Fact]
    public void La_password_demo_e_Demo_punto_anno_corrente_punto_esclamativo()
    {
        Assert.Equal($"Demo.{GestiSoft.Application.Pulizie.PulizieSoggiornoService.Oggi().Year}!", SuperAdminService.PasswordDemo());
    }

    [Fact]
    public void I_numeri_delle_dirette_sono_progressivi_per_anno()
    {
        var dati = StrutturaDemoGenerator.Genera(Oggi, 20260924);

        foreach (var anno in dati.Prenotazioni.Where(p => p.Agenzia == "Diretta").GroupBy(p => p.Anno))
        {
            var numeri = anno.Select(p => int.Parse(p.NumeroPrenotazione!)).Order().ToList();
            Assert.Equal(Enumerable.Range(1, numeri.Count), numeri);
        }
    }
}
