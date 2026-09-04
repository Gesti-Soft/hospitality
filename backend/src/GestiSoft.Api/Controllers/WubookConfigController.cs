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
        return Ok(await ToDtoAsync(integrazione, cancellationToken));
    }

    [HttpPut]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, [FromBody] AggiornaWubookConfigRequest request, CancellationToken cancellationToken)
    {
        var integrazione = await service.AggiornaConfigAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(await ToDtoAsync(integrazione, cancellationToken));
    }

    /// <summary>Lettura completa della licenza (valori segreti inclusi) — solo Super Admin.</summary>
    [HttpGet("/strutture/{strutturaId:guid}/wubook/licenza")]
    public async Task<IActionResult> GetLicenza(Guid strutturaId, CancellationToken cancellationToken)
    {
        var integrazione = await service.GetLicenzaSuperAdminAsync(currentUser, strutturaId, cancellationToken);
        return Ok(ToLicenzaDto(integrazione));
    }

    /// <summary>Licenza Wubook (token/codice struttura/scadenza + username/token gestisoft.it) — solo Super Admin, vedi WubookLicenzaService.</summary>
    [HttpPut("/strutture/{strutturaId:guid}/wubook/licenza")]
    public async Task<IActionResult> AggiornaLicenza(Guid strutturaId, [FromBody] AggiornaWubookLicenzaRequest request, CancellationToken cancellationToken)
    {
        var integrazione = await service.AggiornaLicenzaSuperAdminAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToLicenzaDto(integrazione));
    }

    /// <summary>Prenotazioni Wubook intercettate per questa Struttura (Lcode/Rcode/esito) — visibile a chi può leggere la configurazione Wubook, non solo al Super Admin.</summary>
    [HttpGet("/strutture/{strutturaId:guid}/wubook/eventi-ricevuti")]
    public async Task<IActionResult> GetEventiRicevuti(Guid strutturaId, CancellationToken cancellationToken)
    {
        var eventi = await service.ListEventiRicevutiAsync(currentUser, strutturaId, cancellationToken);
        return Ok(eventi.Select(e => new WubookEventoRicevutoDto(e.Id, e.Lcode, e.Rcode, e.ImportazioneRiuscita, e.MessaggioErrore, e.CreatedAtUtc, e.UpdatedAtUtc)));
    }

    private async Task<WubookIntegrazioneDto> ToDtoAsync(WubookIntegrazione w, CancellationToken cancellationToken) => new(
        w.StrutturaId,
        w.Attivo,
        await service.CredenzialiProntoAsync(w, cancellationToken),
        w.UltimoErrore);

    private static WubookLicenzaDto ToLicenzaDto(WubookIntegrazione w) => new(
        w.StrutturaId,
        w.GestisoftUsername,
        w.GestisoftToken,
        w.CodiceStruttura);
}
