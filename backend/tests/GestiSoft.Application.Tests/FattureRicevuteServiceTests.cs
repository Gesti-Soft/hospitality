using GestiSoft.Application.Auth;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Le fatture ricevute sono del Cliente: da una struttura si vedono quelle di tutte le sue, anche
/// disattivate, mai quelle di un altro Cliente. Aprire il file la segna come letta.
/// </summary>
public class FattureRicevuteServiceTests
{
    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly Struttura Prima = new() { Id = Guid.NewGuid(), ClienteId = Cliente, Nome = "Villa A" };
    private static readonly Struttura Seconda = new() { Id = Guid.NewGuid(), ClienteId = Cliente, Nome = "Villa B", Attivo = false };
    private static readonly Struttura AltroCliente = new() { Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), Nome = "Hotel C" };

    private static FatturaRicevuta Fattura(Struttura s, DateOnly data) =>
        new() { Id = Guid.NewGuid(), StrutturaId = s.Id, FileRicevutoSdiId = Guid.NewGuid(), Fornitore = "Lavanderia", Numero = "1", Data = data };

    private readonly FattureFinte repository = new();

    private FattureRicevuteService Servizio() =>
        new(repository, new StruttureFinte([Prima, Seconda, AltroCliente]), new PermessoStrutturaGuard(new TenantAccessGuard(null!, null!, null!, null!), null!, null!));

    [Fact]
    public async Task Lista_QuelleDiTutteLeStruttureDelCliente_NonDiAltri()
    {
        var a = Fattura(Prima, new DateOnly(2026, 3, 1));
        var b = Fattura(Seconda, new DateOnly(2026, 5, 1));
        repository.Elenco.AddRange([a, b, Fattura(AltroCliente, new DateOnly(2026, 4, 1))]);

        var elenco = await Servizio().ListaAsync(new SuperAdmin(), Prima.Id, 2026, CancellationToken.None);

        Assert.Equal([b.Id, a.Id], elenco.Select(x => x.Fattura.Id));
        Assert.Equal(["Villa B", "Villa A"], elenco.Select(x => x.StrutturaNome));
    }

    [Fact]
    public async Task Lista_FiltraPerAnnoDelDocumento()
    {
        repository.Elenco.AddRange([Fattura(Prima, new DateOnly(2025, 12, 31)), Fattura(Prima, new DateOnly(2026, 1, 1))]);

        var elenco = await Servizio().ListaAsync(new SuperAdmin(), Prima.Id, 2025, CancellationToken.None);

        Assert.Equal(new DateOnly(2025, 12, 31), Assert.Single(elenco).Fattura.Data);
    }

    [Fact]
    public async Task File_DiUnAltroCliente_NonSiTrova()
    {
        var altrui = Fattura(AltroCliente, new DateOnly(2026, 4, 1));
        repository.Elenco.Add(altrui);

        await Assert.ThrowsAsync<GestiSoft.Application.Exceptions.NotFoundException>(() =>
            Servizio().FileAsync(new SuperAdmin(), Prima.Id, altrui.Id, CancellationToken.None));
        Assert.Null(altrui.LettaAtUtc);
    }

    [Fact]
    public async Task File_LaSegnaComeLetta_UnaVoltaSola()
    {
        var f = Fattura(Seconda, new DateOnly(2026, 4, 1));
        repository.Elenco.Add(f);
        repository.File[f.FileRicevutoSdiId] = ("IT09876543210_00001.xml.p7m", [1, 2, 3]);

        var (nome, _) = await Servizio().FileAsync(new SuperAdmin(), Prima.Id, f.Id, CancellationToken.None);
        var letta = f.LettaAtUtc;
        await Servizio().SegnaLettaAsync(new SuperAdmin(), Prima.Id, f.Id, CancellationToken.None);

        Assert.Equal("IT09876543210_00001.xml.p7m", nome);
        Assert.NotNull(letta);
        Assert.Equal(letta, f.LettaAtUtc);
        Assert.Equal(1, repository.Salvataggi);
    }

    /// <summary>Il Super Admin passa i controlli dei permessi senza toccare i repository: qui si prova solo il perimetro del Cliente.</summary>
    private sealed class SuperAdmin : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UtenteId => Guid.Empty;
        public string Email => "admin@example.com";
        public bool IsSuperAdmin => true;
        public Guid? ClienteId => null;
        public bool IsClienteAccount => false;
    }

    private sealed class StruttureFinte(IReadOnlyList<Struttura> strutture) : IStrutturaRepository
    {
        public Task<Guid?> GetClienteIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
            Task.FromResult(strutture.FirstOrDefault(s => s.Id == strutturaId)?.ClienteId);

        public Task<Struttura?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(strutture.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<Struttura>> ListByClienteAsync(Guid? clienteId, bool includiInattive, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Struttura>>(strutture.Where(s => s.ClienteId == clienteId && (includiInattive || s.Attivo)).ToList());

        public Task<IReadOnlyList<Struttura>> ListAssegnateAsync(Guid utenteId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(Struttura struttura, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(Struttura struttura, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FattureFinte : IFatturaRicevutaRepository
    {
        public readonly List<FatturaRicevuta> Elenco = [];
        public readonly Dictionary<Guid, (string, byte[])> File = [];
        public int Salvataggi;

        public Task<IReadOnlyList<FatturaRicevuta>> ListAsync(IReadOnlyCollection<Guid> strutturaIds, int? anno, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FatturaRicevuta>>(Elenco
                .Where(f => strutturaIds.Contains(f.StrutturaId) && (anno is null || f.Data.Year == anno))
                .OrderByDescending(f => f.Data)
                .ToList());

        public Task<IReadOnlyList<int>> ListaAnniAsync(IReadOnlyCollection<Guid> strutturaIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<int>>(Elenco.Where(f => strutturaIds.Contains(f.StrutturaId)).Select(f => f.Data.Year).Distinct().ToList());

        public Task<FatturaRicevuta?> GetAsync(IReadOnlyCollection<Guid> strutturaIds, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Elenco.FirstOrDefault(f => f.Id == id && strutturaIds.Contains(f.StrutturaId)));

        public Task<(string NomeFile, byte[] File)?> GetFileAsync(Guid fileRicevutoSdiId, CancellationToken cancellationToken) =>
            Task.FromResult<(string NomeFile, byte[] File)?>(File.TryGetValue(fileRicevutoSdiId, out var f) ? f : null);

        public Task SalvaAsync(CancellationToken cancellationToken)
        {
            Salvataggi++;
            return Task.CompletedTask;
        }
    }
}
