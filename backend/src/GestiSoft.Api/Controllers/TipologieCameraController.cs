using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Contracts.Camere;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/tipologie-camera")]
[Authorize]
public class TipologieCameraController(CamereService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var tipologie = await service.ListaTipologieAsync(currentUser, strutturaId, cancellationToken);
        return Ok(tipologie.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] CreaTipologiaRequest request, CancellationToken cancellationToken)
    {
        var tipologia = await service.CreaTipologiaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(tipologia));
    }

    [HttpPut("{tipologiaId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid tipologiaId, [FromBody] CreaTipologiaRequest request, CancellationToken cancellationToken)
    {
        var tipologia = await service.AggiornaTipologiaAsync(currentUser, strutturaId, tipologiaId, request, cancellationToken);
        return Ok(ToDto(tipologia));
    }

    [HttpDelete("{tipologiaId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        await service.EliminaTipologiaAsync(currentUser, strutturaId, tipologiaId, cancellationToken);
        return NoContent();
    }

    private static TipologiaCameraDto ToDto(SettingTipologia t) => new(
        t.Id, t.StrutturaId, t.TipologiaCamera, t.SpesePulizia, t.Animali, t.Cauzione,
        t.PrezzoDefault, t.NumeroImplementoPersona, t.Implemento);
}
