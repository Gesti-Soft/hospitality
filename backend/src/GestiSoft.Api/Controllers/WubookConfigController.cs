using GestiSoft.Application.Auth;
using GestiSoft.Application.Wubook;
using GestiSoft.Contracts.Wubook;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/wubook/config")]
[Authorize]
public class WubookConfigController(WubookLicenzaService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid strutturaId, CancellationToken cancellationToken)
    {
        var integrazione = await service.GetOrDefaultAsync(currentUser, strutturaId, cancellationToken);
        return Ok(ToDto(integrazione));
    }

    [HttpPut]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, [FromBody] AggiornaWubookConfigRequest request, CancellationToken cancellationToken)
    {
        var integrazione = await service.AggiornaConfigAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(integrazione));
    }

    /// <summary>Forza un rinnovo immediato delle credenziali (utile subito dopo aver inserito la licenza, senza aspettare il job periodico).</summary>
    [HttpPost("rinnova")]
    public async Task<IActionResult> Rinnova(Guid strutturaId, CancellationToken cancellationToken)
    {
        var integrazione = await service.GetOrDefaultAsync(currentUser, strutturaId, cancellationToken);
        integrazione = await service.RinnovaCredenzialiAsync(integrazione, cancellationToken);
        return Ok(ToDto(integrazione));
    }

    private static WubookIntegrazioneDto ToDto(WubookIntegrazione w) => new(
        w.StrutturaId,
        w.Attivo,
        w.GestisoftUsername,
        LicenzaConfigurata: !string.IsNullOrWhiteSpace(w.GestisoftUsername) && !string.IsNullOrWhiteSpace(w.GestisoftToken),
        CredenzialiPronte: !string.IsNullOrWhiteSpace(w.ApiKeyCache) && !string.IsNullOrWhiteSpace(w.LcodeCache),
        w.CacheAggiornataAtUtc,
        w.UltimoErrore);
}
