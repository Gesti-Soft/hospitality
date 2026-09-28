using GestiSoft.Application.Auth;
using GestiSoft.Application.Trattamenti;
using GestiSoft.Contracts.Trattamenti;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Trattamenti della struttura (colazione, mezza pensione, pensione completa) e buoni colazione delle prenotazioni.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/trattamenti")]
[Authorize]
public class TrattamentiController(TrattamentiService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var trattamenti = await service.ListaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(trattamenti.Select(ToDto));
    }

    /// <summary>Crea o aggiorna il listino di un trattamento: uno per tipo.</summary>
    [HttpPut]
    public async Task<IActionResult> Salva(Guid strutturaId, [FromBody] TrattamentoStrutturaDto request, CancellationToken cancellationToken)
    {
        var trattamento = await service.SalvaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(trattamento));
    }

    /// <summary>PDF dei ticket del trattamento della prenotazione, generato al volo e non conservato.</summary>
    [HttpGet("~/strutture/{strutturaId:guid}/prenotazioni/{prenotazioneId:guid}/buoni-colazione")]
    public async Task<IActionResult> BuoniColazione(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var pdf = await service.BuoniColazioneAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
        return File(pdf, "application/pdf", "ticket.pdf");
    }

    private static TrattamentoStrutturaDto ToDto(TrattamentoStruttura t) => new(
        t.Tipo, t.Attivo, t.PrezzoPerPersona, t.PrezzoBambini, t.TipoPrezzoBambini, t.EtaMassimaBambini, t.EsercizioConvenzionato, t.StampaTicket);
}
