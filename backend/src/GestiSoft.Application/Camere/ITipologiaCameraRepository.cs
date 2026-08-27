using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Camere;

public interface ITipologiaCameraRepository
{
    Task<SettingTipologia?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SettingTipologia>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<bool> ExistsByNomeAsync(Guid strutturaId, string tipologiaCamera, Guid? escludiId, CancellationToken cancellationToken);

    Task AddAsync(SettingTipologia entity, CancellationToken cancellationToken);

    Task UpdateAsync(SettingTipologia entity, CancellationToken cancellationToken);

    Task DeleteAsync(SettingTipologia entity, CancellationToken cancellationToken);
}
