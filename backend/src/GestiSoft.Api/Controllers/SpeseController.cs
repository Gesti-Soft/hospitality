using GestiSoft.Application.Auth;
using GestiSoft.Application.Finanze;
using GestiSoft.Contracts.Finanze;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/spese")]
[Authorize]
public class SpeseController(FinanzeService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var spese = await service.ListaSpeseAsync(currentUser, strutturaId, anno, cancellationToken);
        return Ok(spese.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] CreaSpesaRequest request, CancellationToken cancellationToken)
    {
        var spesa = await service.CreaSpesaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(spesa));
    }

    [HttpPut("{spesaId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid spesaId, [FromBody] CreaSpesaRequest request, CancellationToken cancellationToken)
    {
        var spesa = await service.AggiornaSpesaAsync(currentUser, strutturaId, spesaId, request, cancellationToken);
        return Ok(ToDto(spesa));
    }

    [HttpDelete("{spesaId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid spesaId, CancellationToken cancellationToken)
    {
        await service.EliminaSpesaAsync(currentUser, strutturaId, spesaId, cancellationToken);
        return NoContent();
    }

    private static SpesaDto ToDto(Spesa s) => new(
        s.Id, s.StrutturaId, s.TipoSpesa, s.Nome, s.ImportoSpesa, s.Descrizione, s.MetodoPagamento, s.DataSpesa, s.Anno);
}
