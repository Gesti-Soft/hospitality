using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.PayTourist;

public interface IPayTouristIntegrazioneRepository
{
    Task<PayTouristIntegrazione?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task UpsertAsync(PayTouristIntegrazione entity, CancellationToken cancellationToken);
}
