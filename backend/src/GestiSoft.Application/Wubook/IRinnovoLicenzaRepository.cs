using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public interface IRinnovoLicenzaRepository
{
    Task AddAsync(RinnovoLicenza rinnovo, CancellationToken cancellationToken);
}
