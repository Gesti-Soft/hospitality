using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Camere;

public interface ICameraRepository
{
    Task<SettingRoom?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SettingRoom>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Camere reali di una Tipologia (il "pool") — usata sia per calcolare la quantità inviata a Wubook sia per risolvere la prima camera libera di una prenotazione sulla tipologia.</summary>
    Task<IReadOnlyList<SettingRoom>> ListByTipologiaAsync(Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken);

    Task<bool> ExistsByNomeAsync(Guid strutturaId, string nome, Guid? escludiId, CancellationToken cancellationToken);

    Task AddAsync(SettingRoom entity, CancellationToken cancellationToken);

    Task UpdateAsync(SettingRoom entity, CancellationToken cancellationToken);

    Task DeleteAsync(SettingRoom entity, CancellationToken cancellationToken);
}
