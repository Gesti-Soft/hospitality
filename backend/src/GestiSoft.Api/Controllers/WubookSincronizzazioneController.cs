using GestiSoft.Application.Auth;
using GestiSoft.Application.Wubook;
using GestiSoft.Contracts.Wubook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

public record SincronizzaPeriodoRequest(DateTime DataInizio, DateTime DataFine);

/// <summary>Trigger manuali per le sincronizzazioni Wubook (prezzi, disponibilità, prenotazioni) — le stesse eseguite in automatico dai job Quartz nel Worker, richiamabili a mano da UI per non aspettare il prossimo giro schedulato.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/wubook")]
[Authorize]
public class WubookSincronizzazioneController(
    WubookPrezziService prezziService,
    WubookDisponibilitaService disponibilitaService,
    WubookPrenotazioniService prenotazioniService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("prezzi/sincronizza")]
    public async Task<IActionResult> SincronizzaPrezzi(Guid strutturaId, [FromBody] SincronizzaPeriodoRequest request, CancellationToken cancellationToken)
    {
        await prezziService.SincronizzaAsync(currentUser, strutturaId, request.DataInizio, request.DataFine, cancellationToken);
        return NoContent();
    }

    [HttpPost("disponibilita/sincronizza")]
    public async Task<IActionResult> SincronizzaDisponibilita(Guid strutturaId, [FromBody] SincronizzaPeriodoRequest request, CancellationToken cancellationToken)
    {
        await disponibilitaService.SincronizzaAsync(currentUser, strutturaId, request.DataInizio, request.DataFine, cancellationToken);
        return NoContent();
    }

    [HttpPost("prenotazioni/sincronizza")]
    public async Task<IActionResult> SincronizzaPrenotazioni(Guid strutturaId, CancellationToken cancellationToken)
    {
        var risultato = await prenotazioniService.SincronizzaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(new RisultatoSincronizzazioneDto(risultato.Importate, risultato.Aggiornate, risultato.Annullate, risultato.Errori));
    }
}
