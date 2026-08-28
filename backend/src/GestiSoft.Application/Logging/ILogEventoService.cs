using GestiSoft.Application.Common;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Logging;

public interface ILogEventoService
{
    Task RegistraAsync(
        LivelloLog livello,
        string messaggio,
        string origine,
        string? dettaglio = null,
        string? correlationId = null,
        Guid? clienteId = null,
        Guid? strutturaId = null,
        string? categoria = null,
        string? operatore = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<LogEvento>> CercaAsync(LogEventoFiltro filtro, CancellationToken cancellationToken = default);
}
