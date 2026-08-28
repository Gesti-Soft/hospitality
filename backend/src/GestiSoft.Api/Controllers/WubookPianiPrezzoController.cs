using GestiSoft.Application.Auth;
using GestiSoft.Application.Wubook;
using GestiSoft.Contracts.Wubook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Piani prezzo nominati/virtuali — porta PricingPlansController del legacy. Puro proxy verso Wubook, nessuna persistenza locale.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/wubook/piani-prezzo")]
[Authorize]
public class WubookPianiPrezzoController(WubookPianiPrezzoService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var piani = await service.ListaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(piani.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] CreaPianoPrezzoRequestDto request, CancellationToken cancellationToken)
    {
        var id = await service.CreaAsync(currentUser, strutturaId, new CreaPianoPrezzoRequest(request.Nome, request.ParentId, request.TipoVariazione, request.Variazione), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{pianoId:int}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, int pianoId, [FromBody] AggiornaPianoPrezzoRequestDto request, CancellationToken cancellationToken)
    {
        await service.AggiornaAsync(currentUser, strutturaId, pianoId, new AggiornaPianoPrezzoRequest(request.Nome, request.TipoVariazione, request.Variazione), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{pianoId:int}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, int pianoId, CancellationToken cancellationToken)
    {
        await service.EliminaAsync(currentUser, strutturaId, pianoId, cancellationToken);
        return NoContent();
    }

    private static PianoPrezzoDto ToDto(WubookPianoPrezzo p) => new(p.Id, p.Nome, p.Daily, p.IsVirtual, p.ParentId, p.Variazione, p.TipoVariazione);
}
