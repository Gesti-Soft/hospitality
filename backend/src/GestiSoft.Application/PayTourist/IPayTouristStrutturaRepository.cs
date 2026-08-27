using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.PayTourist;

public interface IPayTouristStrutturaRepository
{
    Task<IReadOnlyList<PayTouristStruttura>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<PayTouristStruttura?> GetAsync(Guid strutturaId, Guid payTouristStrutturaId, CancellationToken cancellationToken);

    Task AddAsync(PayTouristStruttura entity, CancellationToken cancellationToken);

    Task UpdateAsync(PayTouristStruttura entity, CancellationToken cancellationToken);

    void RimuoviTipologia(PayTouristStrutturaTipologia riga);
}
