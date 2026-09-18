using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione;

public interface IDatiFatturaRepository
{
    Task<DatiFattura?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>L'eventuale fattura più recente generata da questa Prenotazione — null se non ancora fatturata (o se creata prima dell'introduzione di questo legame).</summary>
    Task<DatiFattura?> GetByPrenotazioneIdAsync(Guid prenotazioneId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DatiFattura>> ListAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken);

    /// <summary>Anni con almeno una fattura emessa — per il selettore Anno.</summary>
    Task<IReadOnlyList<int>> ListaAnniConDatiAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Ultimo numero usato nella serie indicata: fatture e ricevute hanno numerazioni separate.</summary>
    Task<int> GetMaxProgressivoAsync(Guid strutturaId, int anno, TipoEmissioneDocumento tipoEmissione, CancellationToken cancellationToken);

    /// <summary>
    /// Tenta l'inserimento; restituisce false in caso di conflitto sul vincolo unique
    /// (StrutturaId, Anno, Progressivo) — il chiamante deve ricalcolare il progressivo e
    /// ritentare (protegge dalla race condition assente nel legacy, dove GetProgressiviFattura
    /// non aveva alcun lock: due fatture create in concorrenza potevano ottenere lo stesso numero).
    /// </summary>
    Task<bool> TryAddAsync(DatiFattura entity, CancellationToken cancellationToken);

    Task UpdateAsync(DatiFattura entity, CancellationToken cancellationToken);
}
