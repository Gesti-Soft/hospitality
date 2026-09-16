using GestiSoft.Application.Auth;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Contracts.Fatturazione;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/dati-aziendali")]
[Authorize]
public class DatiAziendaliController(DatiAziendaliService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid strutturaId, CancellationToken cancellationToken)
    {
        var dati = await service.GetOrDefaultAsync(currentUser, strutturaId, cancellationToken);
        return Ok(ToDto(dati));
    }

    [HttpPut]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, [FromBody] AggiornaDatiAziendaliRequest request, CancellationToken cancellationToken)
    {
        var dati = await service.AggiornaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(dati));
    }

    private static DatiAziendaliDto ToDto(Domain.Entities.DatiAziendali d) => new(
        d.StrutturaId, d.Iso2, d.PIva, d.CodiceFiscale, d.Denominazione, d.Nome, d.Cognome,
        d.RegimeFiscale, d.AliquotaIvaDefault, d.NaturaDefault, d.DicituraFattura,
        d.Indirizzo, d.NCivico, d.Cap, d.Comune, d.Provincia, d.Nazione);
}
