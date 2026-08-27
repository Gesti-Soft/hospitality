using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione;

public interface IDatiFatturaRepository
{
    Task<DatiFattura?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DatiFattura>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken);

    Task<int> GetMaxProgressivoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>
    /// Tenta l'inserimento; restituisce false in caso di conflitto sul vincolo unique
    /// (StrutturaId, Anno, Progressivo) — il chiamante deve ricalcolare il progressivo e
    /// ritentare (protegge dalla race condition assente nel legacy, dove GetProgressiviFattura
    /// non aveva alcun lock: due fatture create in concorrenza potevano ottenere lo stesso numero).
    /// </summary>
    Task<bool> TryAddAsync(DatiFattura entity, CancellationToken cancellationToken);

    Task UpdateAsync(DatiFattura entity, CancellationToken cancellationToken);
}
