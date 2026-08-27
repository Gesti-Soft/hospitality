using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Impostazioni;

public interface IImpostazioniStrutturaRepository
{
    Task<ImpostazioniStruttura?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Strutture con l'invio Alloggiati Web attivo — usata dal job Quartz giornaliero (Fase 6).</summary>
    Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerPoliziaAsync(CancellationToken cancellationToken);

    /// <summary>Strutture con l'invio Osservatorio Turistico attivo — usata dal job Quartz giornaliero (Fase 7).</summary>
    Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerOsservatorioAsync(CancellationToken cancellationToken);

    /// <summary>Strutture con l'invio PayTourist attivo — usata dal job Quartz giornaliero (Fase 8).</summary>
    Task<IReadOnlyList<ImpostazioniStruttura>> ListAttivePerPayTouristAsync(CancellationToken cancellationToken);

    Task UpsertAsync(ImpostazioniStruttura impostazioni, CancellationToken cancellationToken);
}
