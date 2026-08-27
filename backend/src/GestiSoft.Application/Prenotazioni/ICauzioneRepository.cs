using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Prenotazioni;

public interface ICauzioneRepository
{
    Task AddAsync(Cauzione entity, CancellationToken cancellationToken);
}
