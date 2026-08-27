using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Contracts.Camere;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/camere")]
[Authorize]
public class CamereController(CamereService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var camere = await service.ListaCamereAsync(currentUser, strutturaId, cancellationToken);
        return Ok(camere.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] CreaCameraRequest request, CancellationToken cancellationToken)
    {
        var camera = await service.CreaCameraAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(camera));
    }

    [HttpPut("{cameraId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid cameraId, [FromBody] CreaCameraRequest request, CancellationToken cancellationToken)
    {
        var camera = await service.AggiornaCameraAsync(currentUser, strutturaId, cameraId, request, cancellationToken);
        return Ok(ToDto(camera));
    }

    [HttpDelete("{cameraId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        await service.EliminaCameraAsync(currentUser, strutturaId, cameraId, cancellationToken);
        return NoContent();
    }

    private static CameraDto ToDto(SettingRoom r) => new(
        r.Id, r.StrutturaId, r.TipologiaId, r.Tipologia?.TipologiaCamera, r.StateRoom, r.Nome,
        r.CapacitaOspiti, r.SoggiornoMinimo);
}
