using GestiSoft.Application.Auth;
using GestiSoft.Application.Osservatorio;
using GestiSoft.Contracts.Osservatorio;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/osservatorio/appartamenti")]
[Authorize]
public class OsservatorioAppartamentiController(OsservatorioConfigService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var appartamenti = await service.ListaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(appartamenti.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] SalvaOsservatorioAppartamentoRequest request, CancellationToken cancellationToken)
    {
        var appartamento = await service.CreaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(appartamento));
    }

    [HttpPut("{appartamentoId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid appartamentoId, [FromBody] SalvaOsservatorioAppartamentoRequest request, CancellationToken cancellationToken)
    {
        var appartamento = await service.AggiornaAsync(currentUser, strutturaId, appartamentoId, request, cancellationToken);
        return Ok(ToDto(appartamento));
    }

    private static OsservatorioAppartamentoDto ToDto(OsservatorioAppartamento a) => new(
        a.Id,
        a.StrutturaId,
        a.Nome,
        a.EntityCode,
        a.HotelCode,
        CredenzialiConfigurate: !string.IsNullOrWhiteSpace(a.EntityCode) && !string.IsNullOrWhiteSpace(a.Password),
        a.Tipologie.Select(t => t.TipologiaId).ToList(),
        a.CursoreDataAtUtc,
        a.UltimoInvioAtUtc,
        a.UltimeSchedineInviate,
        a.UltimoErrore);
}
