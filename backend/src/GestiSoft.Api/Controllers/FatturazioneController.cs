using GestiSoft.Application.Auth;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Contracts.Fatturazione;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/fatture")]
[Authorize]
public class FatturazioneController(FatturazioneService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var fatture = await service.ListaAsync(currentUser, strutturaId, anno, cancellationToken);
        return Ok(fatture.Select(ToDto));
    }

    [HttpGet("{fatturaId:guid}")]
    public async Task<IActionResult> Get(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var fattura = await service.GetAsync(currentUser, strutturaId, fatturaId, cancellationToken);
        return Ok(ToDto(fattura));
    }

    [HttpPost]
    public async Task<IActionResult> CreaDaPrenotazione(Guid strutturaId, [FromBody] CreaFatturaDaPrenotazioneRequest request, CancellationToken cancellationToken)
    {
        var fattura = await service.CreaDaPrenotazioneAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(fattura));
    }

    [HttpPut("{fatturaId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid fatturaId, [FromBody] AggiornaFatturaRequest request, CancellationToken cancellationToken)
    {
        var fattura = await service.AggiornaAsync(currentUser, strutturaId, fatturaId, request, cancellationToken);
        return Ok(ToDto(fattura));
    }

    [HttpGet("{fatturaId:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var pdf = await service.GeneraPdfAsync(currentUser, strutturaId, fatturaId, cancellationToken);
        return File(pdf, "application/pdf", $"fattura-{fatturaId}.pdf");
    }

    [HttpGet("{fatturaId:guid}/xml")]
    public async Task<IActionResult> Xml(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var xml = await service.GeneraXmlSdiAsync(currentUser, strutturaId, fatturaId, cancellationToken);
        return File(xml, "application/xml", $"fattura-{fatturaId}.xml");
    }

    private static DatiFatturaDto ToDto(DatiFattura f) => new(
        f.Id, f.StrutturaId, f.DatiClienteId,
        f.Cliente is null ? null : !string.IsNullOrWhiteSpace(f.Cliente.Denominazione) ? f.Cliente.Denominazione : $"{f.Cliente.Nome} {f.Cliente.Cognome}".Trim(),
        f.Progressivo, f.TipoDocumento, f.RegimeFiscale, f.NumeroDocumento, f.DataDocumento, f.Divisa,
        f.Descrizione, f.Quantita, f.PrezzoUnitario, f.PrezzoTotale, f.ImportoTotale, f.AliquotaIva, f.Natura, f.Anno);
}
