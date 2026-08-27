using GestiSoft.Application.Auth;
using GestiSoft.Application.PayTourist;
using GestiSoft.Contracts.PayTourist;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/paytourist/strutture")]
[Authorize]
public class PayTouristStruttureController(PayTouristConfigService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var strutture = await service.ListaStruttureAsync(currentUser, strutturaId, cancellationToken);
        return Ok(strutture.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] SalvaPayTouristStrutturaRequest request, CancellationToken cancellationToken)
    {
        var struttura = await service.CreaStrutturaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(struttura));
    }

    [HttpPut("{payTouristStrutturaId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid payTouristStrutturaId, [FromBody] SalvaPayTouristStrutturaRequest request, CancellationToken cancellationToken)
    {
        var struttura = await service.AggiornaStrutturaAsync(currentUser, strutturaId, payTouristStrutturaId, request, cancellationToken);
        return Ok(ToDto(struttura));
    }

    private static PayTouristStrutturaDto ToDto(PayTouristStruttura p) => new(
        p.Id,
        p.StrutturaId,
        p.Nome,
        p.IdStrutturaPaytourist,
        p.Tipologie.Select(t => t.TipologiaId).ToList(),
        p.UltimoInvioAtUtc,
        p.UltimeInviate,
        p.UltimoErrore);
}
