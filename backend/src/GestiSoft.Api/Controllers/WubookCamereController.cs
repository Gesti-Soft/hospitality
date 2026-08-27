using GestiSoft.Application.Auth;
using GestiSoft.Application.Wubook;
using GestiSoft.Contracts.Camere;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/wubook/camere")]
[Authorize]
public class WubookCamereController(WubookCamereService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("{cameraId:guid}/sincronizza")]
    public async Task<IActionResult> Sincronizza(Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        var camera = await service.SincronizzaAsync(currentUser, strutturaId, cameraId, cancellationToken);
        return Ok(ToDto(camera));
    }

    [HttpDelete("{cameraId:guid}")]
    public async Task<IActionResult> Rimuovi(Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        var camera = await service.RimuoviAsync(currentUser, strutturaId, cameraId, cancellationToken);
        return Ok(ToDto(camera));
    }

    private static CameraDto ToDto(SettingRoom r) => new(
        r.Id, r.StrutturaId, r.TipologiaId, r.Tipologia?.TipologiaCamera, r.StateRoom, r.Nome,
        r.CapacitaOspiti, r.SoggiornoMinimo, r.IdCameraWubook, r.WubookAttiva);
}
