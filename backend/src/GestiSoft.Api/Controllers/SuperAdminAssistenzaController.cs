using GestiSoft.Application.Assistenza;
using GestiSoft.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Ticket di assistenza di tutti i Clienti, dal pannello Super Admin.</summary>
[ApiController]
[Route("super-admin/assistenza/ticket")]
[Authorize]
public class SuperAdminAssistenzaController(AssistenzaService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista([FromQuery] bool soloAperti, CancellationToken cancellationToken)
    {
        var tickets = await service.ListaTuttiAsync(currentUser, soloAperti, cancellationToken);
        return Ok(tickets.Select(t => AssistenzaMappatura.ToDto(t, perStaff: true)));
    }

    [HttpGet("non-letti/conteggio")]
    public async Task<IActionResult> ContaNonLetti(CancellationToken cancellationToken) =>
        Ok(await service.ContaNonLettiStaffAsync(currentUser, cancellationToken));

    [HttpGet("{ticketId:guid}")]
    public async Task<IActionResult> Dettaglio(Guid ticketId, CancellationToken cancellationToken)
    {
        var dettaglio = await service.DettaglioStaffAsync(currentUser, ticketId, cancellationToken);
        return Ok(AssistenzaMappatura.ToDettaglioDto(dettaglio, perStaff: true));
    }

    [HttpPut("{ticketId:guid}/letto")]
    public async Task<IActionResult> SegnaLetto(Guid ticketId, CancellationToken cancellationToken)
    {
        await service.SegnaLettoStaffAsync(currentUser, ticketId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{ticketId:guid}/messaggi")]
    [RequestSizeLimit(AssistenzaMappatura.LimiteRichiestaByte)]
    [RequestFormLimits(MultipartBodyLengthLimit = AssistenzaMappatura.LimiteRichiestaByte)]
    public async Task<IActionResult> Rispondi(
        Guid ticketId,
        [FromForm] string? testo,
        [FromForm] List<IFormFile>? allegati,
        CancellationToken cancellationToken)
    {
        var foto = await AssistenzaMappatura.LeggiAllegatiAsync(allegati, cancellationToken);
        var dettaglio = await service.RispondiStaffAsync(currentUser, ticketId, testo ?? string.Empty, foto, cancellationToken);
        return Ok(AssistenzaMappatura.ToDettaglioDto(dettaglio, perStaff: true));
    }

    [HttpPut("{ticketId:guid}/chiudi")]
    public async Task<IActionResult> Chiudi(Guid ticketId, CancellationToken cancellationToken)
    {
        var dettaglio = await service.ChiudiAsync(currentUser, ticketId, cancellationToken);
        return Ok(AssistenzaMappatura.ToDettaglioDto(dettaglio, perStaff: true));
    }

    [HttpGet("allegati/{allegatoId:guid}")]
    public async Task<IActionResult> Allegato(Guid allegatoId, CancellationToken cancellationToken)
    {
        var (contenuto, contentType) = await service.ApriAllegatoStaffAsync(currentUser, allegatoId, cancellationToken);
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(contenuto, contentType);
    }
}
