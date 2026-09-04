using GestiSoft.Application.Auth;
using GestiSoft.Application.SuperAdmin;
using GestiSoft.Contracts.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Impostazioni a livello di applicazione (non di Cliente/Struttura), es. l'Id Software richiesto da PayTourist — solo Super Admin.</summary>
[ApiController]
[Route("super-admin/impostazioni-globali")]
[Authorize]
public class ImpostazioniGlobaliController(ImpostazioniGlobaliService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var impostazioni = await service.GetAsync(currentUser, cancellationToken);
        return Ok(new ImpostazioniGlobaliDto(impostazioni.IdSoftwarePaytourist, impostazioni.TokenWubook));
    }

    [HttpPut]
    public async Task<IActionResult> Aggiorna([FromBody] AggiornaImpostazioniGlobaliRequest request, CancellationToken cancellationToken)
    {
        var impostazioni = await service.AggiornaAsync(currentUser, request, cancellationToken);
        return Ok(new ImpostazioniGlobaliDto(impostazioni.IdSoftwarePaytourist, impostazioni.TokenWubook));
    }
}
