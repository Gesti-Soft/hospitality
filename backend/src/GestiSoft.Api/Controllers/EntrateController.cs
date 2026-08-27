using GestiSoft.Application.Auth;
using GestiSoft.Application.Finanze;
using GestiSoft.Contracts.Finanze;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/entrate")]
[Authorize]
public class EntrateController(FinanzeService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var entrate = await service.ListaEntrateAsync(currentUser, strutturaId, anno, cancellationToken);
        return Ok(entrate.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] CreaEntrataRequest request, CancellationToken cancellationToken)
    {
        var entrata = await service.CreaEntrataAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(entrata));
    }

    [HttpPut("{entrataId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid entrataId, [FromBody] CreaEntrataRequest request, CancellationToken cancellationToken)
    {
        var entrata = await service.AggiornaEntrataAsync(currentUser, strutturaId, entrataId, request, cancellationToken);
        return Ok(ToDto(entrata));
    }

    [HttpDelete("{entrataId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid entrataId, CancellationToken cancellationToken)
    {
        await service.EliminaEntrataAsync(currentUser, strutturaId, entrataId, cancellationToken);
        return NoContent();
    }

    private static EntrataDto ToDto(Entrata e) => new(
        e.Id, e.StrutturaId, e.TipoEntrata, e.Nome, e.ImportoEntrata, e.Descrizione, e.Data, e.Anno);
}
