using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Finanze;

public interface IEntrataRepository
{
    Task<Entrata?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Entrata>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken);

    /// <summary>
    /// Somma delle entrate dall'inizio fino all'anno indicato incluso — per la cassa cumulata, che
    /// non deve includere gli anni successivi a quello selezionato. Le righe senza anno sono
    /// sempre incluse: non hanno un anno a cui appartenere, ma i soldi ci sono.
    /// </summary>
    Task<decimal> SommaFinoAdAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>Anni con almeno un'entrata registrata — per il selettore Anno.</summary>
    Task<IReadOnlyList<int>> ListaAnniConDatiAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task AddAsync(Entrata entity, CancellationToken cancellationToken);

    Task UpdateAsync(Entrata entity, CancellationToken cancellationToken);

    Task DeleteAsync(Entrata entity, CancellationToken cancellationToken);
}
