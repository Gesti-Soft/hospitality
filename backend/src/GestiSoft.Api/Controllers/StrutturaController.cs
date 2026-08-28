using GestiSoft.Application.Auth;
using GestiSoft.Application.Strutture;
using GestiSoft.Contracts.Strutture;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture")]
[Authorize]
public class StrutturaController(StrutturaService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? clienteId, CancellationToken cancellationToken)
    {
        var strutture = await service.ListAsync(currentUser, clienteId, cancellationToken);
        return Ok(strutture.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var struttura = await service.GetByIdAsync(currentUser, id, cancellationToken);
        return Ok(ToDto(struttura));
    }

    [HttpPost]
    public async Task<IActionResult> Crea([FromBody] CreaStrutturaRequest request, CancellationToken cancellationToken)
    {
        var struttura = await service.CreaAsync(currentUser, request, cancellationToken);
        var dto = ToDto(struttura);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Aggiorna(Guid id, [FromBody] AggiornaStrutturaRequest request, CancellationToken cancellationToken)
    {
        var struttura = await service.AggiornaAsync(currentUser, id, request, cancellationToken);
        return Ok(ToDto(struttura));
    }

    [HttpPut("{id:guid}/attivo")]
    public async Task<IActionResult> ImpostaAttivo(Guid id, [FromBody] ImpostaAttivoStrutturaRequest request, CancellationToken cancellationToken)
    {
        var struttura = await service.ImpostaAttivoAsync(currentUser, id, request.Attivo, cancellationToken);
        return Ok(new { struttura.Id, struttura.Attivo });
    }

    private static StrutturaDto ToDto(Domain.Entities.Struttura struttura) => new(
        struttura.Id,
        struttura.ClienteId,
        struttura.Nome,
        struttura.CreatedAtUtc,
        struttura.WubookAbilitato,
        struttura.AlloggiatiWebAbilitato,
        struttura.OsservatorioAbilitato,
        struttura.PayTouristAbilitato);
}
