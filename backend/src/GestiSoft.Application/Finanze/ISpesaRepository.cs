using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Finanze;

public interface ISpesaRepository
{
    Task<Spesa?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Spesa>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken);

    /// <summary>Somma delle spese fino all'anno indicato incluso — v. IEntrataRepository.SommaFinoAdAnnoAsync.</summary>
    Task<decimal> SommaFinoAdAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>Anni con almeno una spesa registrata — per il selettore Anno.</summary>
    Task<IReadOnlyList<int>> ListaAnniConDatiAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task AddAsync(Spesa entity, CancellationToken cancellationToken);

    Task UpdateAsync(Spesa entity, CancellationToken cancellationToken);

    Task DeleteAsync(Spesa entity, CancellationToken cancellationToken);
}
