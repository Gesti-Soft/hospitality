using GestiSoft.Application.Auth;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Contracts.Fatturazione;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Le fatture dei fornitori arrivate dallo SdI, di tutte le strutture del Cliente.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/fatture-ricevute")]
[Authorize]
public class FattureRicevuteController(FattureRicevuteService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var elenco = await service.ListaAsync(currentUser, strutturaId, anno, cancellationToken);
        return Ok(elenco.Select(x => new FatturaRicevutaDto(
            x.Fattura.Id, x.Fattura.StrutturaId, x.StrutturaNome, x.Fattura.IdentificativoSdi, x.Fattura.Fornitore,
            x.Fattura.FornitorePartitaIva, x.Fattura.FornitoreCodiceFiscale, x.Fattura.TipoDocumento, x.Fattura.Numero,
            x.Fattura.Data, x.Fattura.Divisa, x.Fattura.ImportoTotale, x.Fattura.Imponibile, x.Fattura.Imposta,
            x.Fattura.RicevutaAtUtc, x.Fattura.LettaAtUtc)));
    }

    [HttpGet("anni")]
    public async Task<IActionResult> AnniDisponibili(Guid strutturaId, CancellationToken cancellationToken)
    {
        var anni = await service.GetAnniDisponibiliAsync(currentUser, strutturaId, cancellationToken);
        return Ok(anni);
    }

    /// <summary>Il file com'è arrivato dallo SdI (XML o .p7m firmato), col nome che gli ha dato lo SdI.</summary>
    [HttpGet("{fatturaRicevutaId:guid}/file")]
    public async Task<IActionResult> Scarica(Guid strutturaId, Guid fatturaRicevutaId, CancellationToken cancellationToken)
    {
        var (nomeFile, file) = await service.FileAsync(currentUser, strutturaId, fatturaRicevutaId, cancellationToken);
        return File(file, "application/octet-stream", nomeFile);
    }

    [HttpPost("{fatturaRicevutaId:guid}/letta")]
    public async Task<IActionResult> SegnaLetta(Guid strutturaId, Guid fatturaRicevutaId, CancellationToken cancellationToken)
    {
        await service.SegnaLettaAsync(currentUser, strutturaId, fatturaRicevutaId, cancellationToken);
        return NoContent();
    }
}
