using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Impostazioni;

public interface IImpostazioniStrutturaRepository
{
    Task<ImpostazioniStruttura?> GetByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task UpsertAsync(ImpostazioniStruttura impostazioni, CancellationToken cancellationToken);
}
