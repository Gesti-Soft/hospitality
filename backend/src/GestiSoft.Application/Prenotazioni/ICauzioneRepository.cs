using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Prenotazioni;

public interface ICauzioneRepository
{
    Task AddAsync(Cauzione entity, CancellationToken cancellationToken);

    /// <summary>Aggiunto in Fase 4 per la vista Finanze — non tocca l'uso esistente di AddAsync dal check-out (Fase 3).</summary>
    Task<IReadOnlyList<Cauzione>> ListByStrutturaAsync(Guid strutturaId, int? anno, CancellationToken cancellationToken);
}
