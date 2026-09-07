using GestiSoft.Application.Auth;
using GestiSoft.Application.Notifiche;
using GestiSoft.Contracts.Notifiche;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>
/// Centro notifiche in-app: visibile a chiunque abbia accesso alla Struttura (TenantAccessGuard),
/// nessun permesso granulare dedicato — è informativo/operativo quotidiano, non una schermata
/// amministrativa (a differenza di /logs, riservato a chi gestisce gli utenti).
/// </summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/notifiche")]
[Authorize]
public class NotificheController(NotificaService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, [FromQuery] bool soloNonLette, CancellationToken cancellationToken)
    {
        var notifiche = await service.ListaAsync(currentUser, strutturaId, soloNonLette, cancellationToken);
        return Ok(notifiche.Select(ToDto));
    }

    [HttpGet("non-lette/conteggio")]
    public async Task<IActionResult> ContaNonLette(Guid strutturaId, CancellationToken cancellationToken)
    {
        var conteggio = await service.ContaNonLetteAsync(currentUser, strutturaId, cancellationToken);
        return Ok(conteggio);
    }

    [HttpPut("{notificaId:guid}/letta")]
    public async Task<IActionResult> SegnaLetta(Guid strutturaId, Guid notificaId, CancellationToken cancellationToken)
    {
        await service.SegnaLettaAsync(currentUser, strutturaId, notificaId, cancellationToken);
        return NoContent();
    }

    [HttpPut("tutte-lette")]
    public async Task<IActionResult> SegnaTutteLette(Guid strutturaId, CancellationToken cancellationToken)
    {
        await service.SegnaTutteLetteAsync(currentUser, strutturaId, cancellationToken);
        return NoContent();
    }

    private static NotificaDto ToDto(Notifica n) => new(n.Id, n.Tipo, n.Titolo, n.Messaggio, n.PrenotazioneId, n.Canale, n.CreatedAtUtc, n.LettaAtUtc);
}
