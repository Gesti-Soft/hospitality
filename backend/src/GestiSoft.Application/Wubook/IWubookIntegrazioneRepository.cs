using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public interface IWubookIntegrazioneRepository
{
    Task<WubookIntegrazione?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WubookIntegrazione>> ListAttiveAsync(CancellationToken cancellationToken);

    Task UpsertAsync(WubookIntegrazione entity, CancellationToken cancellationToken);
}
