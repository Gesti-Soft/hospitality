using GestiSoft.Application.Auth;
using GestiSoft.Application.Wubook;
using GestiSoft.Contracts.Wubook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Piani restrizione nominati — porta RestrictionsController del legacy. Puro proxy verso Wubook, nessuna persistenza locale.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/wubook/piani-restrizione")]
[Authorize]
public class WubookPianiRestrizioneController(WubookPianiRestrizioneService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var piani = await service.ListaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(piani.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] CreaPianoRestrizioneRequestDto request, CancellationToken cancellationToken)
    {
        var id = await service.CreaAsync(currentUser, strutturaId, new CreaPianoRestrizioneRequest(request.Nome, ToRegole(request.Regole)), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{pianoId:int}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, int pianoId, [FromBody] AggiornaPianoRestrizioneRequestDto request, CancellationToken cancellationToken)
    {
        await service.AggiornaAsync(currentUser, strutturaId, pianoId, new AggiornaPianoRestrizioneRequest(request.Nome, ToRegole(request.Regole)), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{pianoId:int}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, int pianoId, CancellationToken cancellationToken)
    {
        await service.EliminaAsync(currentUser, strutturaId, pianoId, cancellationToken);
        return NoContent();
    }

    private static WubookRegoleRestrizione? ToRegole(RegoleRestrizioneDto? r) => r is null
        ? null
        : new WubookRegoleRestrizione(r.MinStay, r.MinStayArrival, r.MaxStay, r.MaxStayArrival, r.Chiuso, r.ChiusoArrivo, r.ChiusoPartenza);

    private static PianoRestrizioneDto ToDto(WubookPianoRestrizione p) => new(
        p.Id, p.Nome,
        p.Regole is { } r ? new RegoleRestrizioneDto(r.MinStay, r.MinStayArrival, r.MaxStay, r.MaxStayArrival, r.Chiuso, r.ChiusoArrivo, r.ChiusoPartenza) : null);
}
