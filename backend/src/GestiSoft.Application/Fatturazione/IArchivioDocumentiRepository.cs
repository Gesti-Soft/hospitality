using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione;

/// <summary>I documenti fiscali delle strutture eliminate. Gli elenchi non si portano dietro PDF e file.</summary>
public interface IArchivioDocumentiRepository
{
    /// <summary>Dal più recente. Cliente null = tutti.</summary>
    Task<IReadOnlyList<DocumentoFiscaleArchiviato>> ListAsync(Guid? clienteId, CancellationToken cancellationToken);

    /// <summary>Con PDF e file.</summary>
    Task<DocumentoFiscaleArchiviato?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Con PDF e file, tutti quelli di un Cliente: per restituirglieli.</summary>
    Task<IReadOnlyList<DocumentoFiscaleArchiviato>> ListCompletiAsync(Guid clienteId, CancellationToken cancellationToken);

    /// <summary>Cancella quelli con la scadenza prima di <paramref name="oggi"/>. Restituisce quanti.</summary>
    Task<int> EliminaScadutiAsync(DateOnly oggi, CancellationToken cancellationToken);
}
