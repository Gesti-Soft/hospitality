using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Osservatorio;

public interface IOsservatorioAppartamentoRepository
{
    Task<IReadOnlyList<OsservatorioAppartamento>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<OsservatorioAppartamento?> GetAsync(Guid strutturaId, Guid appartamentoId, CancellationToken cancellationToken);

    Task AddAsync(OsservatorioAppartamento entity, CancellationToken cancellationToken);

    Task UpdateAsync(OsservatorioAppartamento entity, CancellationToken cancellationToken);

    void RimuoviTipologia(OsservatorioAppartamentoTipologia riga);
}
