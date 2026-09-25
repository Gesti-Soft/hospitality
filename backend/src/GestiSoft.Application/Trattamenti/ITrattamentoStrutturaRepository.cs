using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Trattamenti;

public interface ITrattamentoStrutturaRepository
{
    Task<IReadOnlyList<TrattamentoStruttura>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<TrattamentoStruttura?> GetAsync(Guid strutturaId, TipoTrattamento tipo, CancellationToken cancellationToken);

    Task UpsertAsync(TrattamentoStruttura entity, CancellationToken cancellationToken);
}
