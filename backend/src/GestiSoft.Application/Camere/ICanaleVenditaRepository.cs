using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Camere;

public interface ICanaleVenditaRepository
{
    Task<SettingAgenzia?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SettingAgenzia>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Valori distinti (non normalizzati) del campo Agenzia già presenti sulle prenotazioni della struttura — usato per l'importazione automatica dei canali vendita.</summary>
    Task<IReadOnlyList<string>> ListaAgenzieDistinteDaPrenotazioniAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<bool> ExistsByDescrizioneAsync(Guid strutturaId, string descrizione, Guid? escludiId, CancellationToken cancellationToken);

    Task AddAsync(SettingAgenzia entity, CancellationToken cancellationToken);

    Task UpdateAsync(SettingAgenzia entity, CancellationToken cancellationToken);

    Task DeleteAsync(SettingAgenzia entity, CancellationToken cancellationToken);
}
