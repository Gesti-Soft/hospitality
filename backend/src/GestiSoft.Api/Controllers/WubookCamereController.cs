using GestiSoft.Application.Auth;
using GestiSoft.Application.Wubook;
using GestiSoft.Contracts.Camere;
using GestiSoft.Contracts.Wubook;
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

    /// <summary>Camere locali con stato associazione/disponibilità odierna — per la select Tipologia → camere della tab ridisegnata.</summary>
    [HttpGet("per-associazione")]
    public async Task<IActionResult> ListaPerAssociazione(Guid strutturaId, CancellationToken cancellationToken)
    {
        var lista = await service.ListaPerAssociazioneAsync(currentUser, strutturaId, cancellationToken);
        return Ok(lista.Select(c => new CameraWubookInfoDto(c.CameraId, c.CameraNome, c.TipologiaId, c.TipologiaNome, c.IdCameraWubook, c.WubookAttiva, c.ChiusaOggi, c.ChiusureCount, c.RestrizioniCount)));
    }

    /// <summary>Camere già presenti su Wubook (fetch_rooms), da cui scegliere l'associazione manuale.</summary>
    [HttpGet("remote")]
    public async Task<IActionResult> ListaRemote(Guid strutturaId, CancellationToken cancellationToken)
    {
        var lista = await service.ListaCamereRemoteAsync(currentUser, strutturaId, cancellationToken);
        return Ok(lista.Select(c => new CameraWubookRemoteDto(c.Id, c.Nome, c.ShortName, c.Occupancy, c.Prezzo, c.Disponibilita)));
    }

    /// <summary>Associazione manuale camera-locale ↔ camera-Wubook esistente (nessuna chiamata Wubook, solo salvataggio locale).</summary>
    [HttpPut("{cameraId:guid}/associazione")]
    public async Task<IActionResult> Associa(Guid strutturaId, Guid cameraId, [FromBody] AssociaCameraWubookRequest request, CancellationToken cancellationToken)
    {
        var camera = await service.AssociaAsync(currentUser, strutturaId, cameraId, request.IdCameraWubook, cancellationToken);
        return Ok(ToDto(camera));
    }

    private static CameraDto ToDto(SettingRoom r) => new(
        r.Id, r.StrutturaId, r.TipologiaId, r.Tipologia?.TipologiaCamera, r.StateRoom, r.Nome,
        r.CapacitaOspiti, r.SoggiornoMinimo, r.IdCameraWubook, r.WubookAttiva,
        r.CodiceCameraWubook, r.PrezzoWubookOverride, r.WubookSoloWoodoo);
}

public record AssociaCameraWubookRequest(int? IdCameraWubook);
