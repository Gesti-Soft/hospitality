using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Camere;

public interface ICameraRepository
{
    Task<SettingRoom?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Risolve la camera locale associata a un id camera Wubook — usata dal pull prenotazioni di Fase 5.</summary>
    Task<SettingRoom?> GetByIdWubookAsync(Guid strutturaId, int idCameraWubook, CancellationToken cancellationToken);

    Task<IReadOnlyList<SettingRoom>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<bool> ExistsByNomeAsync(Guid strutturaId, string nome, Guid? escludiId, CancellationToken cancellationToken);

    Task AddAsync(SettingRoom entity, CancellationToken cancellationToken);

    Task UpdateAsync(SettingRoom entity, CancellationToken cancellationToken);

    Task DeleteAsync(SettingRoom entity, CancellationToken cancellationToken);
}
