using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Application.Auth;
using GestiSoft.Contracts.AlloggiatiWeb;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/alloggiati-web/config")]
[Authorize]
public class AlloggiatiWebConfigController(AlloggiatiWebConfigService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid strutturaId, CancellationToken cancellationToken)
    {
        var integrazione = await service.GetOrDefaultAsync(currentUser, strutturaId, cancellationToken);
        return Ok(ToDto(integrazione));
    }

    [HttpPut]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, [FromBody] AggiornaAlloggiatiWebConfigRequest request, CancellationToken cancellationToken)
    {
        var (integrazione, connessioneOk, connessioneErrore) = await service.AggiornaConfigAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(new AggiornaAlloggiatiWebConfigRisultatoDto(ToDto(integrazione), connessioneOk, connessioneErrore));
    }

    private static AlloggiatiWebIntegrazioneDto ToDto(AlloggiatiWebIntegrazione a) => new(
        a.StrutturaId,
        a.Utente,
        CredenzialiConfigurate: !string.IsNullOrWhiteSpace(a.Utente) && !string.IsNullOrWhiteSpace(a.Password) && !string.IsNullOrWhiteSpace(a.WsKey),
        a.UltimoInvioAtUtc,
        a.UltimeSchedineInviate,
        a.UltimoErrore,
        a.UltimaVerificaOkAtUtc);
}
