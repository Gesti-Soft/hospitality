using GestiSoft.Application.Auth;
using GestiSoft.Application.Logging;
using GestiSoft.Contracts.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("logs")]
[Authorize]
public class LogController(ILogEventoService logEventoService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Cerca(
        [FromQuery] Guid? strutturaId,
        [FromQuery] LivelloLog? livello,
        [FromQuery] string? categoria = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // Un Cliente vede solo i propri log, a prescindere da cosa passa in query — solo il
        // Super Admin può vedere/filtrare su tutti i Clienti.
        var clienteId = currentUser.IsSuperAdmin ? null : currentUser.ClienteId;

        var filtro = new LogEventoFiltro(clienteId, strutturaId, livello, categoria, page, pageSize);
        var risultato = await logEventoService.CercaAsync(filtro, cancellationToken);

        return Ok(new PagedResultDto<LogEventoDto>(risultato.Items.Select(ToDto).ToList(), risultato.TotalCount, risultato.Page, risultato.PageSize));
    }

    private static LogEventoDto ToDto(LogEvento e) => new(
        e.Id, e.ClienteId, e.StrutturaId, e.Livello, e.Messaggio, e.Dettaglio, e.CorrelationId, e.Origine, e.CreatedAtUtc, e.Categoria, e.Operatore);
}
