using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Servizi;
using GestiSoft.Application.Trattamenti;
using GestiSoft.Contracts.Servizi;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Servizi extra sulla prenotazione: importo per modalità e per notti della riga, prezzo copiato alla
/// vendita, date dentro il soggiorno, stesso servizio in giorni diversi. Esempio: soggiorno dal 12 al
/// 15 ottobre, 3 notti.
/// </summary>
public class ServiziExtraTests
{
    private static readonly Guid Struttura = Guid.NewGuid();
    private static readonly Guid Prenotazione = Guid.NewGuid();
    private static readonly DateTime Arrivo = new(2026, 10, 12);
    private static readonly DateTime Partenza = new(2026, 10, 15);
    private static DateOnly Giorno(int giorno) => new(2026, 10, giorno);

    [Theory]
    [InlineData(ModalitaPrezzoServizio.APersonaANotte, 5, 2, 30)]
    [InlineData(ModalitaPrezzoServizio.APersona, 40, 2, 80)]
    [InlineData(ModalitaPrezzoServizio.ANotte, 10, 1, 30)]
    [InlineData(ModalitaPrezzoServizio.APrenotazione, 25, 1, 25)]
    public void Importo_SecondoLaModalita(ModalitaPrezzoServizio modalita, int prezzo, int quantita, int atteso)
    {
        Assert.Equal(atteso, ServiziService.Importo(modalita, prezzo, quantita, notti: 3));
    }

    [Fact]
    public void ANotte_ContaLeNottiDellaRiga_NonDelSoggiorno()
    {
        // Parcheggio per 2 notti su 3: dal 13 al 15.
        var riga = new PrenotazioneServizio { Modalita = ModalitaPrezzoServizio.ANotte, PrezzoUnitario = 10m, Quantita = 1, Dal = Giorno(13), Al = Giorno(15) };
        Assert.Equal(20m, ServiziService.Importo(riga));
    }

    [Fact]
    public async Task ServizioNuovo_PrendeIlListino_ELOrigineEAutore()
    {
        var spa = Servizio("SPA", 50m);

        var righe = await Risolvi(new RepositoryFinto([spa]), null, [new(null, spa.Id, 2, Giorno(13))], OrigineServizio.DuranteIlSoggiorno);

        var riga = Assert.Single(righe);
        Assert.Equal(("SPA", 50m, 2, Giorno(13)), (riga.Nome, riga.PrezzoUnitario, riga.Quantita, riga.Dal));
        Assert.Null(riga.Al);
        Assert.Equal(OrigineServizio.DuranteIlSoggiorno, riga.Origine);
        Assert.Equal("reception@example.test", riga.AggiuntoDa);
    }

    [Fact]
    public async Task StessoServizioInGiorniDiversi_DueRighe()
    {
        var spa = Servizio("SPA", 50m);

        var righe = await Risolvi(new RepositoryFinto([spa]), null, [new(null, spa.Id, 2, Giorno(12)), new(null, spa.Id, 1, Giorno(14))]);

        Assert.Equal([Giorno(12), Giorno(14)], righe.Select(r => r.Dal));
        Assert.Equal(150m, righe.Sum(ServiziService.Importo));
    }

    [Fact]
    public async Task RigaGiaVenduta_TienePrezzoOrigineEAutore_AncheSeEliminato()
    {
        var spa = Servizio("SPA", 60m, eliminato: true);
        var repository = new RepositoryFinto([spa]);
        var venduta = new PrenotazioneServizio
        {
            StrutturaId = Struttura,
            PrenotazioneId = Prenotazione,
            ServizioId = spa.Id,
            Nome = "SPA",
            Modalita = ModalitaPrezzoServizio.APersona,
            PrezzoUnitario = 45m,
            Quantita = 2,
            Dal = Giorno(12),
            Origine = OrigineServizio.ConLaPrenotazione,
            AggiuntoDa = "titolare@example.test",
        };
        repository.Righe.Add(venduta);

        var righe = await Risolvi(repository, Prenotazione, [new(venduta.Id, spa.Id, 3, Giorno(13))], OrigineServizio.DuranteIlSoggiorno);

        var riga = Assert.Single(righe);
        Assert.Equal(venduta.Id, riga.Id);
        Assert.Equal((45m, 3, Giorno(13)), (riga.PrezzoUnitario, riga.Quantita, riga.Dal));
        Assert.Equal(OrigineServizio.ConLaPrenotazione, riga.Origine);
        Assert.Equal("titolare@example.test", riga.AggiuntoDa);
    }

    [Fact]
    public async Task RigaDiUnAltraPrenotazione_NonSiTrova()
    {
        var spa = Servizio("SPA", 50m);

        await Assert.ThrowsAsync<NotFoundException>(() => Risolvi(new RepositoryFinto([spa]), Prenotazione, [new(Guid.NewGuid(), spa.Id, 1, Giorno(12))]));
    }

    [Theory]
    [InlineData(11)]
    [InlineData(16)]
    public async Task DataFuoriDalSoggiorno_Rifiutata(int giorno)
    {
        var transfer = Servizio("Transfer", 30m);

        await Assert.ThrowsAsync<ConflictException>(() => Risolvi(new RepositoryFinto([transfer]), null, [new(null, transfer.Id, 1, Giorno(giorno))]));
    }

    [Fact]
    public async Task IlGiornoDellaPartenza_Ammesso()
    {
        var transfer = Servizio("Transfer", 30m);

        var righe = await Risolvi(new RepositoryFinto([transfer]), null, [new(null, transfer.Id, 1, Giorno(15))]);

        Assert.Equal(Giorno(15), Assert.Single(righe).Dal);
    }

    [Theory]
    [InlineData(12, 16)]
    [InlineData(11, 13)]
    [InlineData(13, 13)]
    public async Task NottiFuoriDalSoggiornoOVuote_Rifiutate(int dal, int al)
    {
        var parcheggio = Servizio("Parcheggio", 10m, ModalitaPrezzoServizio.ANotte);

        await Assert.ThrowsAsync<ConflictException>(() => Risolvi(new RepositoryFinto([parcheggio]), null, [new(null, parcheggio.Id, 1, Giorno(dal), Giorno(al))]));
    }

    [Fact]
    public async Task ANotteSenzaAl_Rifiutato()
    {
        var parcheggio = Servizio("Parcheggio", 10m, ModalitaPrezzoServizio.ANotte);

        await Assert.ThrowsAsync<ConflictException>(() => Risolvi(new RepositoryFinto([parcheggio]), null, [new(null, parcheggio.Id, 1, Giorno(12))]));
    }

    [Fact]
    public async Task ServizioNonOfferto_NonSiAggiunge()
    {
        var transfer = Servizio("Transfer", 30m, attivo: false);

        await Assert.ThrowsAsync<ConflictException>(() => Risolvi(new RepositoryFinto([transfer]), null, [new(null, transfer.Id, 1, Giorno(12))]));
    }

    [Fact]
    public async Task ServizioDiUnAltraStruttura_NonSiTrova()
    {
        var altrui = Servizio("Parcheggio", 10m);
        altrui.StrutturaId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => Risolvi(new RepositoryFinto([altrui]), null, [new(null, altrui.Id, 1, Giorno(12))]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task QuantitaFuoriLimite_Rifiutata(int quantita)
    {
        var spa = Servizio("SPA", 50m);

        await Assert.ThrowsAsync<ConflictException>(() => Risolvi(new RepositoryFinto([spa]), null, [new(null, spa.Id, quantita, Giorno(12))]));
    }

    [Fact]
    public void StrutturaNuova_HaIQuattroTrattamentiNonOfferti()
    {
        var trattamenti = TrattamentiService.BaseDellaStruttura(Struttura).ToList();

        Assert.Equal(
            [TipoTrattamento.Colazione, TipoTrattamento.MezzaPensione, TipoTrattamento.PensioneCompleta, TipoTrattamento.AllInclusive],
            trattamenti.Select(t => t.Tipo));
        Assert.All(trattamenti, t => Assert.False(t.Attivo));
        Assert.All(trattamenti, t => Assert.Equal(Struttura, t.StrutturaId));
    }

    private static Task<List<PrenotazioneServizio>> Risolvi(
        RepositoryFinto repository,
        Guid? prenotazioneId,
        IReadOnlyList<ServizioPrenotazioneRichiesta> richieste,
        OrigineServizio origine = OrigineServizio.ConLaPrenotazione) =>
        Service(repository).RisolviRigheAsync(Struttura, prenotazioneId, richieste, Arrivo, Partenza, origine, "reception@example.test", CancellationToken.None);

    private static ServizioStruttura Servizio(
        string nome,
        decimal prezzo,
        ModalitaPrezzoServizio modalita = ModalitaPrezzoServizio.APersona,
        bool attivo = true,
        bool eliminato = false) => new()
    {
        StrutturaId = Struttura,
        Nome = nome,
        Prezzo = prezzo,
        Modalita = modalita,
        Attivo = attivo,
        Eliminato = eliminato,
    };

    // RisolviRigheAsync non usa i controlli di accesso: li fanno i metodi pubblici che la chiamano.
    private static ServiziService Service(RepositoryFinto repository) => new(repository, null!, null!, null!, null!, null!);

    private sealed class RepositoryFinto(List<ServizioStruttura> servizi) : IServizioStrutturaRepository
    {
        public List<PrenotazioneServizio> Righe { get; } = [];

        public Task<IReadOnlyList<ServizioStruttura>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ServizioStruttura>>(servizi.Where(s => s.StrutturaId == strutturaId && !s.Eliminato).ToList());

        public Task<ServizioStruttura?> GetAsync(Guid strutturaId, Guid servizioId, CancellationToken cancellationToken) =>
            Task.FromResult(servizi.FirstOrDefault(s => s.StrutturaId == strutturaId && s.Id == servizioId));

        public Task UpsertAsync(ServizioStruttura entity, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<PrenotazioneServizio>> ListByPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PrenotazioneServizio>>(Righe.Where(r => r.StrutturaId == strutturaId && r.PrenotazioneId == prenotazioneId).ToList());

        public Task SostituisciDellaPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, IReadOnlyList<PrenotazioneServizio> righe, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
