using System.IO.Compression;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Common;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Archivio dei documenti fiscali delle strutture eliminate: scadenza a 10 anni, solo Super Admin,
/// ogni lettura nel Log, restituzione al Cliente in uno zip.
/// </summary>
public class ArchivioDocumentiServiceTests
{
    private readonly ArchivioFinto archivio = new();
    private readonly LogFinto log = new();

    private ArchivioDocumentiService Servizio() => new(archivio, null!, null!, null!, log);

    private static DocumentoFiscaleArchiviato Documento(Guid clienteId, string numero, byte[]? pdf = null, byte[]? file = null, string? nomeFile = null) => new()
    {
        ClienteId = clienteId,
        ClienteRagioneSociale = "Rossi S.r.l.",
        StrutturaIdOriginale = Guid.NewGuid(),
        StrutturaNome = "Villa Rosa",
        Tipo = TipoDocumentoArchiviato.FatturaEmessa,
        Numero = numero,
        Data = new DateOnly(2026, 3, 1),
        Pdf = pdf,
        File = file,
        NomeFile = nomeFile,
        Scadenza = DocumentoFiscaleArchiviato.ScadenzaPer(new DateOnly(2026, 3, 1)),
    };

    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 12, 31)]
    public void Scadenza_31DicembreDelDecimoAnno(int anno, int mese, int giorno) =>
        Assert.Equal(new DateOnly(2036, 12, 31), DocumentoFiscaleArchiviato.ScadenzaPer(new DateOnly(anno, mese, giorno)));

    [Fact]
    public async Task SoloIlSuperAdmin()
    {
        var d = Documento(Guid.NewGuid(), "1/2026", pdf: [1]);
        archivio.Elenco.Add(d);

        await Assert.ThrowsAsync<ForbiddenException>(() => Servizio().ListaAsync(new Utente(false), null, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => Servizio().ScaricaAsync(new Utente(false), d.Id, pdf: true, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => Servizio().ZipClienteAsync(new Utente(false), d.ClienteId, CancellationToken.None));
        Assert.Empty(log.Messaggi);
    }

    [Fact]
    public async Task Scarica_RestaNelLogConChiLHaFatto()
    {
        var d = Documento(Guid.NewGuid(), "12/2026", pdf: [1, 2], file: [3], nomeFile: "IT01234567890_01000.xml");
        archivio.Elenco.Add(d);

        var (nomePdf, pdf) = await Servizio().ScaricaAsync(new Utente(true), d.Id, pdf: true, CancellationToken.None);
        var (nomeXml, xml) = await Servizio().ScaricaAsync(new Utente(true), d.Id, pdf: false, CancellationToken.None);

        Assert.Equal("2026-03-01-fattura-12-2026.pdf", nomePdf);
        Assert.Equal([1, 2], pdf);
        Assert.Equal("IT01234567890_01000.xml", nomeXml);
        Assert.Equal([3], xml);
        Assert.Equal(2, log.Messaggi.Count);
        Assert.All(log.Messaggi, m => Assert.Equal(("admin@example.com", (Guid?)d.ClienteId), (m.Operatore, m.ClienteId)));
    }

    [Fact]
    public async Task Scarica_FileCheNonCe_NonTrovato()
    {
        var d = Documento(Guid.NewGuid(), "3/2026", pdf: [1]);
        archivio.Elenco.Add(d);

        await Assert.ThrowsAsync<NotFoundException>(() => Servizio().ScaricaAsync(new Utente(true), d.Id, pdf: false, CancellationToken.None));
    }

    [Fact]
    public async Task Zip_TuttiIDocumentiDelCliente_NomiDoppiConSuffisso()
    {
        var cliente = Guid.NewGuid();
        var a = Documento(cliente, "1/2026", pdf: [1], file: [2], nomeFile: "IT01234567890_01000.xml");
        var b = Documento(cliente, "1/2026", pdf: [3]);
        b.StrutturaIdOriginale = a.StrutturaIdOriginale;
        archivio.Elenco.AddRange([a, b, Documento(Guid.NewGuid(), "9/2026", pdf: [9])]);

        var (_, contenuto) = await Servizio().ZipClienteAsync(new Utente(true), cliente, CancellationToken.None);

        using var zip = new ZipArchive(new MemoryStream(contenuto));
        var cartella = $"Villa-Rosa-{a.StrutturaIdOriginale.ToString()[..8]}";
        Assert.Equal(
            [$"{cartella}/2026-03-01-fattura-1-2026.pdf", $"{cartella}/IT01234567890_01000.xml", $"{cartella}/2026-03-01-fattura-1-2026-2.pdf"],
            zip.Entries.Select(e => e.FullName));
        Assert.Single(log.Messaggi);
    }

    [Fact]
    public async Task EliminaScaduti_PassaLaDataDiOggi()
    {
        await Servizio().EliminaScadutiAsync(CancellationToken.None);

        Assert.NotNull(archivio.EliminatiPrimaDel);
    }

    private sealed class Utente(bool superAdmin) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UtenteId => Guid.Empty;
        public string Email => "admin@example.com";
        public bool IsSuperAdmin => superAdmin;
        public Guid? ClienteId => null;
        public bool IsClienteAccount => false;
    }

    private sealed class ArchivioFinto : IArchivioDocumentiRepository
    {
        public readonly List<DocumentoFiscaleArchiviato> Elenco = [];
        public DateOnly? EliminatiPrimaDel;

        public Task<IReadOnlyList<DocumentoFiscaleArchiviato>> ListAsync(Guid? clienteId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DocumentoFiscaleArchiviato>>(Elenco.Where(d => clienteId is null || d.ClienteId == clienteId).ToList());

        public Task<DocumentoFiscaleArchiviato?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Elenco.FirstOrDefault(d => d.Id == id));

        public Task<IReadOnlyList<DocumentoFiscaleArchiviato>> ListCompletiAsync(Guid clienteId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DocumentoFiscaleArchiviato>>(Elenco.Where(d => d.ClienteId == clienteId).ToList());

        public Task<int> EliminaScadutiAsync(DateOnly oggi, CancellationToken cancellationToken)
        {
            EliminatiPrimaDel = oggi;
            return Task.FromResult(0);
        }
    }

    private sealed class LogFinto : ILogEventoService
    {
        public readonly List<(string Messaggio, string? Operatore, Guid? ClienteId)> Messaggi = [];

        public Task RegistraAsync(LivelloLog livello, string messaggio, string origine, string? dettaglio = null, string? correlationId = null,
            Guid? clienteId = null, Guid? strutturaId = null, string? categoria = null, string? operatore = null, CancellationToken cancellationToken = default)
        {
            Messaggi.Add((messaggio, operatore, clienteId));
            return Task.CompletedTask;
        }

        public Task<PagedResult<LogEvento>> CercaAsync(LogEventoFiltro filtro, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> PulisciVecchiAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
