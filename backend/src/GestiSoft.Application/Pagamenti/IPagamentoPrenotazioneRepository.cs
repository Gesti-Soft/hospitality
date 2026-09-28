using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Pagamenti;

public interface IPagamentoPrenotazioneRepository
{
    Task<IReadOnlyList<PagamentoPrenotazione>> ListByPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken);

    Task<PagamentoPrenotazione?> GetAsync(Guid strutturaId, Guid pagamentoId, CancellationToken cancellationToken);

    Task AddAsync(PagamentoPrenotazione pagamento, CancellationToken cancellationToken);

    Task UpdateAsync(PagamentoPrenotazione pagamento, CancellationToken cancellationToken);

    Task DeleteAsync(PagamentoPrenotazione pagamento, CancellationToken cancellationToken);

    /// <summary>Incassi meno rimborsi con data nell'anno.</summary>
    Task<decimal> SommaAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>Incassi meno rimborsi con data fino all'anno compreso.</summary>
    Task<decimal> SommaFinoAdAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> ListaAnniAsync(Guid strutturaId, CancellationToken cancellationToken);
}
