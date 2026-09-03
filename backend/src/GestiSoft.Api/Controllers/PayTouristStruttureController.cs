using GestiSoft.Application.Auth;
using GestiSoft.Application.PayTourist;
using GestiSoft.Contracts.PayTourist;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/paytourist/strutture")]
[Authorize]
public class PayTouristStruttureController(PayTouristConfigService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var strutture = await service.ListaStruttureAsync(currentUser, strutturaId, cancellationToken);
        return Ok(strutture.Select(ToDto));
    }

    /// <summary>Strutture abilitate su PayTourist per il Token già configurato — per farle scegliere all'operatore invece di digitare a mano lo structure_id.</summary>
    [HttpGet("disponibili")]
    public async Task<IActionResult> Disponibili(Guid strutturaId, CancellationToken cancellationToken)
    {
        var strutture = await service.ListaStruttureDisponibiliAsync(currentUser, strutturaId, cancellationToken);
        return Ok(strutture);
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] SalvaPayTouristStrutturaRequest request, CancellationToken cancellationToken)
    {
        var (struttura, connessioneOk, connessioneErrore) = await service.CreaStrutturaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(new SalvaPayTouristStrutturaRisultatoDto(ToDto(struttura), connessioneOk, connessioneErrore));
    }

    [HttpPut("{payTouristStrutturaId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid payTouristStrutturaId, [FromBody] SalvaPayTouristStrutturaRequest request, CancellationToken cancellationToken)
    {
        var (struttura, connessioneOk, connessioneErrore) = await service.AggiornaStrutturaAsync(currentUser, strutturaId, payTouristStrutturaId, request, cancellationToken);
        return Ok(new SalvaPayTouristStrutturaRisultatoDto(ToDto(struttura), connessioneOk, connessioneErrore));
    }

    [HttpDelete("{payTouristStrutturaId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid payTouristStrutturaId, CancellationToken cancellationToken)
    {
        await service.EliminaStrutturaAsync(currentUser, strutturaId, payTouristStrutturaId, cancellationToken);
        return NoContent();
    }

    private static PayTouristStrutturaDto ToDto(PayTouristStruttura p) => new(
        p.Id,
        p.StrutturaId,
        p.Nome,
        p.IdStrutturaPaytourist,
        p.Tipologie.Select(t => t.TipologiaId).ToList(),
        p.UltimoInvioAtUtc,
        p.UltimeInviate,
        p.UltimoErrore,
        p.UltimaVerificaOkAtUtc);
}
