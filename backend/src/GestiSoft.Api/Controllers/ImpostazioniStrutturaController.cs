using GestiSoft.Application.Auth;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Contracts.Impostazioni;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/impostazioni")]
[Authorize]
public class ImpostazioniStrutturaController(ImpostazioniStrutturaService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid strutturaId, CancellationToken cancellationToken)
    {
        var impostazioni = await service.GetOrDefaultAsync(currentUser, strutturaId, cancellationToken);
        return Ok(ToDto(impostazioni));
    }

    [HttpPut]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, [FromBody] AggiornaImpostazioniRequest request, CancellationToken cancellationToken)
    {
        var impostazioni = await service.AggiornaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(impostazioni));
    }

    private static ImpostazioniStrutturaDto ToDto(Domain.Entities.ImpostazioniStruttura impostazioni) => new(
        impostazioni.StrutturaId,
        impostazioni.PoliziaStatoAttiva,
        impostazioni.OsservatorioAttivo,
        impostazioni.PayTouristAttivo,
        impostazioni.OraInvioGiornaliero,
        impostazioni.TassaSoggiornoPrezzo,
        impostazioni.TassaSoggiornoMaxGiorni,
        impostazioni.TassaSoggiornoEtaEsenzioneMinori,
        impostazioni.TassaSoggiornoEtaEsenzioneAnziani,
        impostazioni.TassaSoggiornoPercentualeResidenti,
        impostazioni.TassaSoggiornoPercentualeMinori,
        impostazioni.TassaSoggiornoPercentualeAnziani,
        impostazioni.ComuneAttivita);
}
