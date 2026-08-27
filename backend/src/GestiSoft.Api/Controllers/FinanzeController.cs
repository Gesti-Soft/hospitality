using GestiSoft.Application.Auth;
using GestiSoft.Application.Finanze;
using GestiSoft.Contracts.Finanze;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/finanze")]
[Authorize]
public class FinanzeController(FinanzeService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("cauzioni")]
    public async Task<IActionResult> Cauzioni(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var cauzioni = await service.ListaCauzioniAsync(currentUser, strutturaId, anno, cancellationToken);
        return Ok(cauzioni.Select(ToDto));
    }

    [HttpGet("riepilogo-cassa")]
    public async Task<IActionResult> RiepilogoCassa(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var riepilogo = await service.RiepilogoCassaAsync(currentUser, strutturaId, anno ?? DateTime.UtcNow.Year, cancellationToken);
        return Ok(new RiepilogoCassaDto(riepilogo.Anno, riepilogo.ImportoPagatoPrenotazioni, riepilogo.Cauzioni, riepilogo.Entrate, riepilogo.Spese, riepilogo.Saldo));
    }

    private static CauzioneDto ToDto(Cauzione c) => new(c.Id, c.StrutturaId, c.PrenotazioneId, c.ImportoCauzione, c.DataInserimento);
}
