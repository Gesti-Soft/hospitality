using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.SuperAdmin;

public interface IImpostazioniGlobaliRepository
{
    /// <summary>Unica riga della tabella — null se non è mai stata salvata nessuna impostazione globale.</summary>
    Task<ImpostazioniGlobali?> GetAsync(CancellationToken cancellationToken);

    Task UpsertAsync(ImpostazioniGlobali entity, CancellationToken cancellationToken);
}
