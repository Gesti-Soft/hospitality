using GestiSoft.Application.Auth;
using GestiSoft.Application.Wubook;
using GestiSoft.Contracts.Camere;
using GestiSoft.Contracts.Wubook;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/wubook/tipologie")]
[Authorize]
public class WubookTipologieController(WubookCamereService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("{tipologiaId:guid}/sincronizza")]
    public async Task<IActionResult> Sincronizza(Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        var tipologia = await service.SincronizzaAsync(currentUser, strutturaId, tipologiaId, cancellationToken);
        return Ok(ToDto(tipologia));
    }

    [HttpDelete("{tipologiaId:guid}")]
    public async Task<IActionResult> Rimuovi(Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        var tipologia = await service.RimuoviAsync(currentUser, strutturaId, tipologiaId, cancellationToken);
        return Ok(ToDto(tipologia));
    }

    /// <summary>Elimina da OTA un pool che non ha (o non ha più) una Tipologia locale associata — a differenza di Rimuovi sopra, qui non c'è alcuna SettingTipologia da aggiornare.</summary>
    [HttpDelete("remote/{idCameraWubook:int}")]
    public async Task<IActionResult> RimuoviRemota(Guid strutturaId, int idCameraWubook, CancellationToken cancellationToken)
    {
        await service.RimuoviRemotoAsync(currentUser, strutturaId, idCameraWubook, cancellationToken);
        return NoContent();
    }

    /// <summary>Tipologie della struttura con conteggio camere reali collegate e stato associazione OTA.</summary>
    [HttpGet("per-associazione")]
    public async Task<IActionResult> ListaPerAssociazione(Guid strutturaId, CancellationToken cancellationToken)
    {
        var lista = await service.ListaPerAssociazioneAsync(currentUser, strutturaId, cancellationToken);
        return Ok(lista.Select(t => new TipologiaWubookInfoDto(t.TipologiaId, t.TipologiaNome, t.CamereCollegate, t.ChiusureCount, t.RestrizioniCount, t.IdCameraWubook, t.WubookAttiva)));
    }

    /// <summary>Camere già presenti su Wubook (fetch_rooms), da cui scegliere l'associazione manuale.</summary>
    [HttpGet("remote")]
    public async Task<IActionResult> ListaRemote(Guid strutturaId, CancellationToken cancellationToken)
    {
        var lista = await service.ListaCamereRemoteAsync(currentUser, strutturaId, cancellationToken);
        return Ok(lista.Select(c => new CameraWubookRemoteDto(c.Id, c.Nome, c.ShortName, c.Occupancy, c.Prezzo, c.Disponibilita)));
    }

    /// <summary>Associazione manuale tipologia-locale ↔ camera-Wubook esistente (nessuna chiamata Wubook, solo salvataggio locale).</summary>
    [HttpPut("{tipologiaId:guid}/associazione")]
    public async Task<IActionResult> Associa(Guid strutturaId, Guid tipologiaId, [FromBody] AssociaCameraWubookRequest request, CancellationToken cancellationToken)
    {
        var tipologia = await service.AssociaAsync(currentUser, strutturaId, tipologiaId, request.IdCameraWubook, cancellationToken);
        return Ok(ToDto(tipologia));
    }

    private static TipologiaCameraDto ToDto(SettingTipologia t) => new(
        t.Id, t.StrutturaId, t.TipologiaCamera, t.SpesePulizia, t.Animali, t.Cauzione,
        t.PrezzoDefault, t.NumeroImplementoPersona, t.Implemento,
        t.IdCameraWubook, t.WubookAttiva, t.CodiceCameraWubook, t.WubookSoloWoodoo);
}

public record AssociaCameraWubookRequest(int? IdCameraWubook);
