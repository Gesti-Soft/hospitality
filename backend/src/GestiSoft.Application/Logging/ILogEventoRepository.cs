using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Logging;

public interface ILogEventoRepository
{
    Task AddAsync(LogEvento evento, CancellationToken cancellationToken);

    Task<(IReadOnlyList<LogEvento> Items, int TotalCount)> SearchAsync(LogEventoFiltro filtro, CancellationToken cancellationToken);

    /// <summary>Cancellazione netta (non anonimizzazione) delle righe più vecchie delle soglie di conservazione — vedi LogEventoService.</summary>
    Task<int> EliminaPrecedentiAsync(DateTime sogliaInfoUtc, DateTime sogliaAltriUtc, CancellationToken cancellationToken);
}
