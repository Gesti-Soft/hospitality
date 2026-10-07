using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione;

/// <summary>Le fatture dei fornitori arrivate dallo SdI, lette per le strutture di un Cliente.</summary>
public interface IFatturaRicevutaRepository
{
    /// <summary>Dalla più recente per data del documento. Anno null = tutti.</summary>
    Task<IReadOnlyList<FatturaRicevuta>> ListAsync(IReadOnlyCollection<Guid> strutturaIds, int? anno, CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> ListaAnniAsync(IReadOnlyCollection<Guid> strutturaIds, CancellationToken cancellationToken);

    /// <summary>Tracciata: per segnarla come letta.</summary>
    Task<FatturaRicevuta?> GetAsync(IReadOnlyCollection<Guid> strutturaIds, Guid id, CancellationToken cancellationToken);

    /// <summary>Il file com'è arrivato dallo SdI (XML, .p7m o zip), col suo nome.</summary>
    Task<(string NomeFile, byte[] File)?> GetFileAsync(Guid fileRicevutoSdiId, CancellationToken cancellationToken);

    Task SalvaAsync(CancellationToken cancellationToken);
}
