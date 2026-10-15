using GestiSoft.Application.Auth;
using GestiSoft.Application.Common;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Fatturazione;

namespace GestiSoft.Api.Tests;

/// <summary>
/// La spedizione dal canale: cosa diventa la fattura dopo ogni risposta possibile dello SdI, e
/// quale file parte. Repository in memoria, generatore e schema veri.
/// </summary>
public class SdiInvioTests
{
    private readonly Guid strutturaId = Guid.NewGuid();
    private readonly FatturaFinta fatture = new();
    private readonly TrasmissioniFinte trasmissioni = new();
    private readonly ClientFinto client = new();
    private readonly CanaleFinto canale = new();
    private readonly Struttura struttura;
    private readonly ImpostazioniStruttura impostazioni;
    private readonly DatiFattura fattura;

    public SdiInvioTests()
    {
        struttura = new Struttura { Id = strutturaId, SdiAbilitato = true };
        impostazioni = new ImpostazioniStruttura { StrutturaId = strutturaId, SdiAttivo = true };
        var cliente = new DatiCliente
        {
            StrutturaId = strutturaId, Iso2 = "IT", CodiceFiscale = "RSSMRA80A01H501U", Nome = "Mario", Cognome = "Rossi",
            Indirizzo = "Via Milano", Cap = "00100", LuogoResidenza = "ROMA", Provincia = "RM",
        };
        fatture.Clienti[cliente.Id] = cliente;
        fattura = new DatiFattura
        {
            StrutturaId = strutturaId, DatiClienteId = cliente.Id, Progressivo = 3, NumeroDocumento = 3, Anno = 2026,
            DataDocumento = new DateTime(2026, 10, 7), Divisa = "EUR", TipoDocumento = TipoDocumentoFattura.TD01_Fattura,
            RegimeFiscale = RegimeFiscale.RF01_Ordinario, StatoSdi = StatoSdiFattura.DaInviare,
            Righe = [new RigaFattura { Numero = 1, Descrizione = "Soggiorno", Quantita = 1, PrezzoUnitario = 100, PrezzoTotale = 100, AliquotaIva = AliquotaIva.Iva10 }],
            PrezzoTotale = 100, ImportoTotale = 110,
        };
        fatture.Fatture[fattura.Id] = fattura;
    }

    private SdiInvioService Servizio()
    {
        var strutture = new StruttureFinte(struttura);
        var fileSdi = new FileSdiService(fatture, fatture, new AziendaFinta(strutturaId), trasmissioni, new FatturaDocumentGenerator(), new CanaleFinto());
        return new SdiInvioService(fatture, strutture, new ImpostazioniFinte(impostazioni), new ConcessioneServiziGuard(strutture), fileSdi, trasmissioni, client, canale, new LogFinto());
    }

    private Task<EsitoInvioSdi> Invia() => Servizio().InviaAsync(strutturaId, fattura.Id, "operatore@test.it", CancellationToken.None);

    [Fact]
    public async Task InProduzione_LaFatturaDiventaInviata_ConIlNomeFileDelCanale()
    {
        var esito = await Invia();

        Assert.False(esito.Prova);
        Assert.Equal("ITZMMGTN97E08G273L_01000.xml", esito.NomeFile);
        Assert.Equal(StatoSdiFattura.Inviata, fattura.StatoSdi);
        Assert.Equal("111", fattura.IdentificativoSdi);
        Assert.Equal(EsitoTrasmissioneSdi.Inviato, trasmissioni.Elenco.Single().Esito);
        Assert.Equal("ITZMMGTN97E08G273L_01000.xml", client.NomiInviati.Single());
    }

    /// <summary>In collaudo il file parte davvero, ma la fattura non deve risultare trasmessa.</summary>
    [Fact]
    public async Task InCollaudo_LaFatturaRestaDaInviare()
    {
        client.Prova = true;

        var esito = await Invia();

        Assert.True(esito.Prova);
        Assert.Equal(StatoSdiFattura.DaInviare, fattura.StatoSdi);
        Assert.Null(fattura.IdentificativoSdi);
        Assert.True(trasmissioni.Elenco.Single().Prova);
    }

    /// <summary>Ambiente di test con gli esiti applicati: in collaudo la fattura fa il giro di produzione.</summary>
    [Fact]
    public async Task InCollaudo_ConEsitiApplicati_LaFatturaDiventaInviata()
    {
        client.Prova = true;
        canale.CollaudoApplicaEsiti = true;

        var esito = await Invia();

        Assert.False(esito.Prova);
        Assert.Equal(StatoSdiFattura.Inviata, fattura.StatoSdi);
        Assert.NotNull(fattura.IdentificativoSdi);
        Assert.True(trasmissioni.Elenco.Single().Prova);
    }

    /// <summary>EI02: il file non è entrato. Riprovando parte lo stesso file, senza consumare un altro progressivo.</summary>
    [Fact]
    public async Task Rifiutato_SiRiprovaConLoStessoFile()
    {
        client.Errore = "EI02";
        var errore = await Assert.ThrowsAsync<ConflictException>(Invia);
        Assert.Contains("EI02", errore.Message);
        Assert.Equal(StatoSdiFattura.DaInviare, fattura.StatoSdi);

        client.Errore = null;
        await Invia();

        Assert.Single(trasmissioni.Elenco);
        Assert.Equal(["ITZMMGTN97E08G273L_01000.xml", "ITZMMGTN97E08G273L_01000.xml"], client.NomiInviati);
    }

    /// <summary>
    /// Chiamata caduta: il file può essere arrivato. Si riprova con lo stesso nome, così un eventuale
    /// doppione viene scartato dallo SdI invece di diventare una seconda fattura.
    /// </summary>
    [Fact]
    public async Task EsitoIgnoto_SiRiprovaConLoStessoFile()
    {
        client.Cade = true;
        var errore = await Assert.ThrowsAsync<ConflictException>(Invia);
        Assert.Contains("portale", errore.Message);
        Assert.Equal(EsitoTrasmissioneSdi.EsitoIgnoto, trasmissioni.Elenco.Single().Esito);

        client.Cade = false;
        await Invia();

        Assert.Single(trasmissioni.Elenco);
        Assert.Equal(StatoSdiFattura.Inviata, fattura.StatoSdi);
    }

    /// <summary>Dopo uno scarto il nome file è bruciato: si rigenera con un progressivo nuovo, stesso numero di fattura.</summary>
    [Fact]
    public async Task DopoLoScarto_ParteUnFileNuovo()
    {
        await Invia();
        var primo = trasmissioni.Elenco.Single();
        primo.Esito = EsitoTrasmissioneSdi.Scartato;
        fattura.StatoSdi = StatoSdiFattura.Scartata;

        var esito = await Invia();

        Assert.Equal("ITZMMGTN97E08G273L_01001.xml", esito.NomeFile);
        Assert.Equal(3, fattura.NumeroDocumento);
        Assert.Equal(2, trasmissioni.Elenco.Count);
    }

    [Fact]
    public async Task GiaInviata_NonSiRispedisce()
    {
        await Invia();

        await Assert.ThrowsAsync<ConflictException>(Invia);
        Assert.Single(client.NomiInviati);
    }

    /// <summary>Le fatture di prima del canale possono essere già state caricate a mano: rispedirle farebbe un doppione.</summary>
    [Fact]
    public async Task FatturaDiPrimaDelCanale_NonSiSpedisce()
    {
        fattura.StatoSdi = null;

        await Assert.ThrowsAsync<ConflictException>(Invia);
        Assert.Empty(client.NomiInviati);
    }

    [Fact]
    public async Task ServizioNonConcesso_NonSiSpedisce()
    {
        struttura.SdiAbilitato = false;

        await Assert.ThrowsAsync<ForbiddenException>(Invia);
        Assert.Empty(client.NomiInviati);
    }

    [Fact]
    public async Task InterruttoreSpento_NonSiSpedisce()
    {
        impostazioni.SdiAttivo = false;

        await Assert.ThrowsAsync<ConflictException>(Invia);
        Assert.Empty(client.NomiInviati);
    }

    /// <summary>Scaricato per il caricamento a mano: trasmittente e contatore sono quelli della struttura.</summary>
    [Fact]
    public async Task FileScaricatoAMano_PortaLaStrutturaComeTrasmittente()
    {
        var fileSdi = new FileSdiService(fatture, fatture, new AziendaFinta(strutturaId), trasmissioni, new FatturaDocumentGenerator(), new CanaleFinto());

        var file = await fileSdi.PreparaAsync(fattura, viaCanale: false, CancellationToken.None);

        Assert.Equal("IT01234567890_01000.xml", file.NomeFile);
        Assert.False(file.ViaCanale);
    }

    private sealed class CanaleFinto : IConfigurazioneCanaleSdi
    {
        public string? CodiceFiscaleTrasmittente => "ZMMGTN97E08G273L";
        public int ProgressivoIniziale => 1000;
        public string? CodiceDestinatario => null;
        public bool Collaudo => true;
        public bool CollaudoApplicaEsiti { get; set; }
    }

    private sealed class ClientFinto : ISdiTrasmissioneClient
    {
        public bool Prova { get; set; }
        public string? Errore { get; set; }
        public bool Cade { get; set; }
        public List<string> NomiInviati { get; } = [];
        public bool AmbienteDiProva => Prova;

        public Task<RispostaRiceviFile> InviaAsync(string nomeFile, byte[] contenuto, CancellationToken cancellationToken)
        {
            NomiInviati.Add(nomeFile);
            if (Cade)
            {
                throw new SdiNonRaggiungibileException("timeout");
            }

            return Task.FromResult(Errore is null
                ? new RispostaRiceviFile("111", DateTimeOffset.UtcNow, null)
                : new RispostaRiceviFile(null, null, Errore));
        }
    }

    private sealed class TrasmissioniFinte : ITrasmissioneFatturaRepository
    {
        private readonly Dictionary<string, int> contatori = [];
        public List<TrasmissioneFattura> Elenco { get; } = [];

        public Task<TrasmissioneFattura?> GetUltimaAsync(Guid fatturaId, CancellationToken cancellationToken) =>
            Task.FromResult(Elenco.Where(t => t.DatiFatturaId == fatturaId).LastOrDefault());

        public Task<IReadOnlyList<TrasmissioneFattura>> ListByFatturaAsync(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrasmissioneFattura>>(Elenco.Where(t => t.DatiFatturaId == fatturaId).Reverse().ToList());

        public Task<TrasmissioneFattura?> GetAsync(Guid strutturaId, Guid trasmissioneId, CancellationToken cancellationToken) =>
            Task.FromResult(Elenco.FirstOrDefault(t => t.Id == trasmissioneId));

        public Task<IReadOnlyList<RicevutaSdi>> RicevuteByFatturaAsync(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RicevutaSdi?> GetRicevutaAsync(Guid strutturaId, Guid ricevutaId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddAsync(TrasmissioneFattura trasmissione, CancellationToken cancellationToken)
        {
            Elenco.Add(trasmissione);
            return Task.CompletedTask;
        }

        public Task SalvaAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlySet<Guid>> FattureConXmlScaricatoAsync(Guid strutturaId, IReadOnlyCollection<Guid> fatturaIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(Elenco.Where(t => !t.ViaCanale && fatturaIds.Contains(t.DatiFatturaId)).Select(t => t.DatiFatturaId).ToHashSet());

        public Task<int> ProssimoProgressivoInvioAsync(string codiceFiscale, int valoreIniziale, CancellationToken cancellationToken)
        {
            contatori[codiceFiscale] = contatori.TryGetValue(codiceFiscale, out var ultimo) ? ultimo + 1 : valoreIniziale;
            return Task.FromResult(contatori[codiceFiscale]);
        }
    }

    private sealed class FatturaFinta : IDatiFatturaRepository, IDatiClienteRepository
    {
        public Dictionary<Guid, DatiFattura> Fatture { get; } = [];
        public Dictionary<Guid, DatiCliente> Clienti { get; } = [];

        public Task<DatiFattura?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Fatture.GetValueOrDefault(id));
        Task<DatiCliente?> IDatiClienteRepository.GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Clienti.GetValueOrDefault(id));

        public Task<DatiFattura?> GetByPrenotazioneIdAsync(Guid prenotazioneId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DatiFattura>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> ListaAnniConDatiAsync(Guid strutturaId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetMaxProgressivoAsync(Guid strutturaId, int anno, TipoEmissioneDocumento tipoEmissione, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> TryAddAsync(DatiFattura entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(DatiFattura entity, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SostituisciRigheAsync(DatiFattura entity, List<RigaFattura> righe, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<decimal> TotaleStornatoAsync(Guid fatturaId, Guid? escludiNotaId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<RigaFatturata>> ListRigheFatturateAsync(Guid strutturaId, IReadOnlyCollection<Guid> prenotazioneIds, Guid? escludiFatturaId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DatiCliente?> GetByCustomerKeyAsync(Guid strutturaId, string customerKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DatiCliente>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(DatiCliente entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(DatiCliente entity, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AziendaFinta(Guid strutturaId) : IDatiAziendaliRepository
    {
        public Task<DatiAziendali?> GetByStrutturaIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<DatiAziendali?>(new DatiAziendali
        {
            StrutturaId = strutturaId, Iso2 = "IT", PIva = "01234567890", CodiceFiscale = "01234567890", Denominazione = "Hotel Belvedere S.r.l.",
            RegimeFiscale = RegimeFiscale.RF01_Ordinario, Indirizzo = "Via Roma", Cap = "91014", Comune = "CASTELLAMMARE DEL GOLFO", Provincia = "TP",
        });

        public Task UpsertAsync(DatiAziendali entity, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StruttureFinte(Struttura struttura) : IStrutturaRepository
    {
        public Task<Guid?> GetClienteIdAsync(Guid strutturaId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(struttura.ClienteId);
        public Task<Struttura?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Struttura?>(id == struttura.Id ? struttura : null);
        public Task<IReadOnlyList<Struttura>> ListByClienteAsync(Guid? clienteId, bool includiInattive, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Struttura>> ListAssegnateAsync(Guid utenteId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(Struttura s, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Struttura s, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ImpostazioniFinte(ImpostazioniStruttura impostazioni) : IImpostazioniStrutturaRepository
    {
        public Task<ImpostazioniStruttura?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) => Task.FromResult<ImpostazioniStruttura?>(impostazioni);
        public Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerPoliziaAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerOsservatorioAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerPayTouristAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpsertAsync(ImpostazioniStruttura i, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class LogFinto : ILogEventoService
    {
        public Task RegistraAsync(LivelloLog livello, string messaggio, string origine, string? dettaglio = null, string? correlationId = null,
            Guid? clienteId = null, Guid? strutturaId = null, string? categoria = null, string? operatore = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PagedResult<LogEvento>> CercaAsync(LogEventoFiltro filtro, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> PulisciVecchiAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
