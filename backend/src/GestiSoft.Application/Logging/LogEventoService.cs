using GestiSoft.Application.Common;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Logging;

public class LogEventoService(ILogEventoRepository repository) : ILogEventoService
{
    public async Task RegistraAsync(
        LivelloLog livello,
        string messaggio,
        string origine,
        string? dettaglio = null,
        string? correlationId = null,
        Guid? clienteId = null,
        Guid? strutturaId = null,
        string? categoria = null,
        string? operatore = null,
        CancellationToken cancellationToken = default)
    {
        var evento = new LogEvento
        {
            Livello = livello,
            Messaggio = messaggio,
            Origine = origine,
            Dettaglio = dettaglio,
            CorrelationId = correlationId,
            ClienteId = clienteId,
            StrutturaId = strutturaId,
            Categoria = categoria,
            Operatore = operatore,
        };

        await repository.AddAsync(evento, cancellationToken);
    }

    public async Task<PagedResult<LogEvento>> CercaAsync(LogEventoFiltro filtro, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.SearchAsync(filtro, cancellationToken);
        return new PagedResult<LogEvento>(items, totalCount, filtro.Page, filtro.PageSize);
    }
}
