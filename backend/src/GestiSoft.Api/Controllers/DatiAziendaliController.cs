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

    /// <summary>
    /// Il logo si legge da qui e non dentro il DTO dei dati fiscali: separandolo, aprire la
    /// fatturazione non si porta dietro un'immagine ad ogni richiesta, e il browser puo' tenerla
    /// in cache per conto suo.
    /// </summary>
    [HttpGet("logo")]
    public async Task<IActionResult> Logo(Guid strutturaId, CancellationToken cancellationToken)
    {
        var logo = await service.GetLogoAsync(currentUser, strutturaId, cancellationToken);
        if (logo is not { } l)
        {
            return NotFound();
        }

        return File(l.Contenuto, l.ContentType);
    }

    [HttpPut("logo")]
    [RequestSizeLimit(DatiAziendaliService.LogoMaxByte + 8 * 1024)]
    public async Task<IActionResult> CaricaLogo(Guid strutturaId, IFormFile file, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        await file.CopyToAsync(memoria, cancellationToken);

        var dati = await service.AggiornaLogoAsync(currentUser, strutturaId, memoria.ToArray(), cancellationToken);
        return Ok(ToDto(dati));
    }

    [HttpDelete("logo")]
    public async Task<IActionResult> RimuoviLogo(Guid strutturaId, CancellationToken cancellationToken)
    {
        await service.RimuoviLogoAsync(currentUser, strutturaId, cancellationToken);
        return NoContent();
    }

    private static DatiAziendaliDto ToDto(Domain.Entities.DatiAziendali d) => new(
        d.StrutturaId, d.Iso2, d.PIva, d.CodiceFiscale, d.Denominazione, d.Nome, d.Cognome,
        d.RegimeFiscale, d.AliquotaIvaDefault, d.NaturaDefault, d.DicituraFattura,
        d.Logo is { Length: > 0 },
        d.Indirizzo, d.NCivico, d.Cap, d.Comune, d.Provincia, d.Nazione);
}
