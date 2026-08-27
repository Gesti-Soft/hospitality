using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Osservatorio;

public interface IOsservatorioInvioRepository
{
    Task<IReadOnlyList<OsservatorioInvio>> ListByPrenotazioneAsync(Guid prenotazioneId, CancellationToken cancellationToken);

    Task AddRangeAsync(IReadOnlyList<OsservatorioInvio> righe, CancellationToken cancellationToken);
}
