using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.AlloggiatiWeb;

public interface IAlloggiatiWebIntegrazioneRepository
{
    Task<AlloggiatiWebIntegrazione?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task UpsertAsync(AlloggiatiWebIntegrazione entity, CancellationToken cancellationToken);
}
