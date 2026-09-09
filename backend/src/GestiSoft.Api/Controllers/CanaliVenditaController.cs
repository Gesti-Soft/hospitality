using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Contracts.Camere;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/canali-vendita")]
[Authorize]
public class CanaliVenditaController(CanaliVenditaService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var canali = await service.ListaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(canali.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] CreaCanaleVenditaRequest request, CancellationToken cancellationToken)
    {
        var canale = await service.CreaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(canale));
    }

    [HttpPost("importa-da-prenotazioni")]
    public async Task<IActionResult> ImportaDaPrenotazioni(Guid strutturaId, CancellationToken cancellationToken)
    {
        var creati = await service.ImportaDaPrenotazioniAsync(currentUser, strutturaId, cancellationToken);
        return Ok(creati.Select(ToDto));
    }

    [HttpPut("{canaleId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid canaleId, [FromBody] CreaCanaleVenditaRequest request, CancellationToken cancellationToken)
    {
        var canale = await service.AggiornaAsync(currentUser, strutturaId, canaleId, request, cancellationToken);
        return Ok(ToDto(canale));
    }

    [HttpDelete("{canaleId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid canaleId, CancellationToken cancellationToken)
    {
        await service.EliminaAsync(currentUser, strutturaId, canaleId, cancellationToken);
        return NoContent();
    }

    private static CanaleVenditaDto ToDto(SettingAgenzia a) => new(a.Id, a.StrutturaId, a.Descrizione, a.Colore);
}
