using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione;

public interface IDatiAziendaliRepository
{
    Task<DatiAziendali?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task UpsertAsync(DatiAziendali entity, CancellationToken cancellationToken);
}
