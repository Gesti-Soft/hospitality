using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione;

public interface IDatiFatturaRepository
{
    Task<DatiFattura?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>L'eventuale documento più recente che fattura questa Prenotazione (anche insieme ad altre) — null se non ancora fatturata.</summary>
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

    /// <summary>Le righe del documento diventano esattamente queste.</summary>
    Task SostituisciRigheAsync(DatiFattura entity, List<RigaFattura> righe, CancellationToken cancellationToken);

    /// <summary>
    /// Le righe di soggiorno e di servizio già fatturate per queste prenotazioni, in qualunque documento
    /// tranne quello indicato (in modifica si esclude se stesso): servono a non fatturare due volte.
    /// </summary>
    Task<IReadOnlyList<RigaFatturata>> ListRigheFatturateAsync(Guid strutturaId, IReadOnlyCollection<Guid> prenotazioneIds, Guid? escludiFatturaId, CancellationToken cancellationToken);
}

/// <summary>Una riga già fatturata e il documento che la contiene.</summary>
public record RigaFatturata(Guid PrenotazioneId, TipoRigaFattura Tipo, Guid? PrenotazioneServizioId, TipoEmissioneDocumento TipoEmissione, int NumeroDocumento, int Anno);
