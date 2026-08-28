using GestiSoft.Application.Auth;
using GestiSoft.Application.Wubook;
using GestiSoft.Contracts.Wubook;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Chiusure camera per periodo (manutenzione, ecc.) — porta RoomsController.closures del legacy. Il push verso Wubook avviene tramite "Sincronizza disponibilità" (WubookSincronizzazioneController), non qui.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/wubook/camere/{cameraId:guid}/chiusure")]
[Authorize]
public class WubookChiusureController(WubookChiusureService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        var chiusure = await service.ListaAsync(currentUser, strutturaId, cameraId, cancellationToken);
        return Ok(chiusure.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, Guid cameraId, [FromBody] CreaChiusuraCameraRequestDto request, CancellationToken cancellationToken)
    {
        var entity = await service.CreaAsync(currentUser, strutturaId, new CreaChiusuraCameraRequest(cameraId, request.DataInizio, request.DataFine, request.Motivo, request.Quantita), cancellationToken);
        return Ok(ToDto(entity));
    }

    [HttpDelete("{chiusuraId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid cameraId, Guid chiusuraId, CancellationToken cancellationToken)
    {
        await service.EliminaAsync(currentUser, strutturaId, chiusuraId, cancellationToken);
        return NoContent();
    }

    private static ChiusuraCameraDto ToDto(ChiusuraCamera c) => new(c.Id, c.CameraId, c.DataInizio, c.DataFine, c.Motivo, c.Quantita);
}
