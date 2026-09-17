using GestiSoft.Application.Auth;
using GestiSoft.Application.PayTourist;
using GestiSoft.Contracts.PayTourist;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/paytourist/config")]
[Authorize]
public class PayTouristConfigController(PayTouristConfigService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid strutturaId, CancellationToken cancellationToken)
    {
        var integrazione = await service.GetConfigAsync(currentUser, strutturaId, cancellationToken);
        return Ok(ToDto(integrazione));
    }

    [HttpPut]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, [FromBody] AggiornaPayTouristConfigRequest request, CancellationToken cancellationToken)
    {
        var (integrazione, verificaOk, verificaErrore) = await service.AggiornaConfigAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(new SalvaPayTouristConfigRisultatoDto(ToDto(integrazione), verificaOk, verificaErrore));
    }

    /// <summary>Suggerimento (da confermare in UI, mai salvato in automatico) per le soglie età di Impostazioni → Tassa di soggiorno, letto da GET api/v1/reductions.</summary>
    [HttpGet("suggerimento-eta-tassa")]
    public async Task<IActionResult> SuggerimentoEtaTassa(Guid strutturaId, CancellationToken cancellationToken)
    {
        var suggerimento = await service.SuggerisciEtaEsenzioneTassaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(suggerimento);
    }

    /// <summary>Chiesto dalla checkbox "Filtra per portale online" prima di lasciarla attivare: se l'ente non prevede l'incasso da portali, l'opzione blocca gli invii invece di filtrarli.</summary>
    [HttpGet("portali-online")]
    public async Task<IActionResult> VerificaPortaliOnline(Guid strutturaId, CancellationToken cancellationToken)
    {
        var esito = await service.VerificaPortaliOnlineAsync(currentUser, strutturaId, cancellationToken);
        return Ok(esito);
    }

    private static PayTouristIntegrazioneDto ToDto(PayTouristIntegrazione p) => new(
        p.StrutturaId,
        TokenConfigurato: !string.IsNullOrWhiteSpace(p.Token),
        p.PortaleOnlineAttivo,
        p.PortaliAttivi.OrderBy(x => x.Nome).Select(x => new PayTouristPortaleOnlineDto(x.IdPortale, x.Nome)).ToList());
}
