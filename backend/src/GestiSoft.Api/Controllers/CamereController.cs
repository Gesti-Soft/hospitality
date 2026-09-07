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

    /// <summary>Segna una camera come pulita (torna "Pronta") — pagina Pulizie, permesso RoomStatusUpdate.</summary>
    [HttpPut("{cameraId:guid}/pulita")]
    public async Task<IActionResult> SegnaPulita(Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        var camera = await service.SegnaPulitaAsync(currentUser, strutturaId, cameraId, cancellationToken);
        return Ok(ToDto(camera));
    }

    /// <summary>Duplica tipologie/camere/prezzi/canali vendita da un'altra Struttura attiva dello stesso Cliente.</summary>
    [HttpPost("duplica-da/{strutturaOrigineId:guid}")]
    public async Task<IActionResult> DuplicaDa(Guid strutturaId, Guid strutturaOrigineId, CancellationToken cancellationToken)
    {
        var risultato = await service.DuplicaDaAsync(currentUser, strutturaId, strutturaOrigineId, cancellationToken);
        return Ok(risultato);
    }

    private static CameraDto ToDto(SettingRoom r) => new(
        r.Id, r.StrutturaId, r.TipologiaId, r.Tipologia?.TipologiaCamera, r.StateRoom, r.Nome,
        r.CapacitaOspiti, r.SoggiornoMinimo, r.IdCameraWubook, r.WubookAttiva,
        r.CodiceCameraWubook, r.PrezzoWubookOverride, r.WubookSoloWoodoo);
}
