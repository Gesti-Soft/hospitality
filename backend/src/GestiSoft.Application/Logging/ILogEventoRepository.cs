using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Logging;

public interface ILogEventoRepository
{
    Task AddAsync(LogEvento evento, CancellationToken cancellationToken);

    Task<(IReadOnlyList<LogEvento> Items, int TotalCount)> SearchAsync(LogEventoFiltro filtro, CancellationToken cancellationToken);
}
