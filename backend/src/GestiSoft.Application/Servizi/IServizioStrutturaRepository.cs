using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Servizi;

public interface IServizioStrutturaRepository
{
    /// <summary>Senza gli eliminati.</summary>
    Task<IReadOnlyList<ServizioStruttura>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Anche se eliminato: serve a chi lo ha già venduto.</summary>
    Task<ServizioStruttura?> GetAsync(Guid strutturaId, Guid servizioId, CancellationToken cancellationToken);

    Task UpsertAsync(ServizioStruttura entity, CancellationToken cancellationToken);

    Task<IReadOnlyList<PrenotazioneServizio>> ListByPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken);

    /// <summary>Le righe della prenotazione diventano esattamente queste.</summary>
    Task SostituisciDellaPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, IReadOnlyList<PrenotazioneServizio> righe, CancellationToken cancellationToken);
}
