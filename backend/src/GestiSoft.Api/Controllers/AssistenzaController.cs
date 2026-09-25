using GestiSoft.Application.Assistenza;
using GestiSoft.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>
/// Ticket di assistenza dal lato della struttura: solo il titolare e chi gestisce gli utenti
/// (permesso SettingUser), vedi AssistenzaService.
/// </summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/assistenza/ticket")]
[Authorize]
public class AssistenzaController(AssistenzaService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var tickets = await service.ListaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(tickets.Select(t => AssistenzaMappatura.ToDto(t, perStaff: false)));
    }

    [HttpGet("non-letti/conteggio")]
    public async Task<IActionResult> ContaNonLetti(Guid strutturaId, CancellationToken cancellationToken) =>
        Ok(await service.ContaNonLettiAsync(currentUser, strutturaId, cancellationToken));

    [HttpGet("{ticketId:guid}")]
    public async Task<IActionResult> Dettaglio(Guid strutturaId, Guid ticketId, CancellationToken cancellationToken)
    {
        var dettaglio = await service.DettaglioAsync(currentUser, strutturaId, ticketId, cancellationToken);
        return Ok(AssistenzaMappatura.ToDettaglioDto(dettaglio, perStaff: false));
    }

    [HttpPut("{ticketId:guid}/letto")]
    public async Task<IActionResult> SegnaLetto(Guid strutturaId, Guid ticketId, CancellationToken cancellationToken)
    {
        await service.SegnaLettoAsync(currentUser, strutturaId, ticketId, cancellationToken);
        return NoContent();
    }

    [HttpPost]
    [RequestSizeLimit(AssistenzaMappatura.LimiteRichiestaByte)]
    [RequestFormLimits(MultipartBodyLengthLimit = AssistenzaMappatura.LimiteRichiestaByte)]
    public async Task<IActionResult> Apri(
        Guid strutturaId,
        [FromForm] string? oggetto,
        [FromForm] string? testo,
        [FromForm] List<IFormFile>? allegati,
        CancellationToken cancellationToken)
    {
        var foto = await AssistenzaMappatura.LeggiAllegatiAsync(allegati, cancellationToken);
        var dettaglio = await service.ApriAsync(currentUser, strutturaId, oggetto ?? string.Empty, testo ?? string.Empty, foto, cancellationToken);
        return Ok(AssistenzaMappatura.ToDettaglioDto(dettaglio, perStaff: false));
    }

    [HttpPost("{ticketId:guid}/messaggi")]
    [RequestSizeLimit(AssistenzaMappatura.LimiteRichiestaByte)]
    [RequestFormLimits(MultipartBodyLengthLimit = AssistenzaMappatura.LimiteRichiestaByte)]
    public async Task<IActionResult> Rispondi(
        Guid strutturaId,
        Guid ticketId,
        [FromForm] string? testo,
        [FromForm] List<IFormFile>? allegati,
        CancellationToken cancellationToken)
    {
        var foto = await AssistenzaMappatura.LeggiAllegatiAsync(allegati, cancellationToken);
        var dettaglio = await service.RispondiAsync(currentUser, strutturaId, ticketId, testo ?? string.Empty, foto, cancellationToken);
        return Ok(AssistenzaMappatura.ToDettaglioDto(dettaglio, perStaff: false));
    }

    [HttpGet("allegati/{allegatoId:guid}")]
    public async Task<IActionResult> Allegato(Guid strutturaId, Guid allegatoId, CancellationToken cancellationToken)
    {
        var (contenuto, contentType) = await service.ApriAllegatoAsync(currentUser, strutturaId, allegatoId, cancellationToken);
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(contenuto, contentType);
    }
}
