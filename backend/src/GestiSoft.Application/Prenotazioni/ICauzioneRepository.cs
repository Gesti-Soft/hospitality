using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Prenotazioni;

public interface ICauzioneRepository
{
    Task AddAsync(Cauzione entity, CancellationToken cancellationToken);

    /// <summary>Aggiunto in Fase 4 per la vista Finanze — non tocca l'uso esistente di AddAsync dal check-out (Fase 3).</summary>
    Task<IReadOnlyList<Cauzione>> ListByStrutturaAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken);

    /// <summary>Somma delle cauzioni trattenute fino all'anno indicato incluso — v. IEntrataRepository.SommaFinoAdAnnoAsync.</summary>
    Task<decimal> SommaFinoAdAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>Anni con almeno una cauzione trattenuta — per il selettore Anno.</summary>
    Task<IReadOnlyList<int>> ListaAnniConDatiAsync(Guid strutturaId, CancellationToken cancellationToken);
}
