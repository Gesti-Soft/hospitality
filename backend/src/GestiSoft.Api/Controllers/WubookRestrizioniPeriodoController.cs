using GestiSoft.Application.Auth;
using GestiSoft.Application.Wubook;
using GestiSoft.Contracts.Wubook;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Soggiorno minimo/massimo per camera valido solo in un periodo — porta RoomsController.stay-restrictions del legacy. Il push verso Wubook avviene tramite "Sincronizza disponibilità".</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/wubook/camere/{cameraId:guid}/restrizioni-periodo")]
[Authorize]
public class WubookRestrizioniPeriodoController(WubookRestrizioniPeriodoService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        var restrizioni = await service.ListaAsync(currentUser, strutturaId, cameraId, cancellationToken);
        return Ok(restrizioni.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, Guid cameraId, [FromBody] CreaRestrizioneSoggiornoCameraRequestDto request, CancellationToken cancellationToken)
    {
        var entity = await service.CreaAsync(currentUser, strutturaId, new CreaRestrizioneSoggiornoCameraRequest(cameraId, request.DataInizio, request.DataFine, request.MinStay, request.MaxStay, request.Motivo), cancellationToken);
        return Ok(ToDto(entity));
    }

    [HttpDelete("{restrizioneId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid cameraId, Guid restrizioneId, CancellationToken cancellationToken)
    {
        await service.EliminaAsync(currentUser, strutturaId, restrizioneId, cancellationToken);
        return NoContent();
    }

    private static RestrizioneSoggiornoCameraDto ToDto(RestrizioneSoggiornoCamera r) => new(r.Id, r.CameraId, r.DataInizio, r.DataFine, r.MinStay, r.MaxStay, r.Motivo);
}
