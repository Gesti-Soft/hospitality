using System.Text;
using GestiSoft.Application.Auth;
using GestiSoft.Application.PayTourist;
using GestiSoft.Contracts.PayTourist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Invio manuale a PayTourist — la stessa azione eseguita in automatico dal job Quartz giornaliero nel Worker, richiamabile a mano da UI senza aspettare l'orario configurato.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/paytourist")]
[Authorize]
public class PayTouristSincronizzazioneController(PayTouristInvioService invioService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("invia")]
    public async Task<IActionResult> InviaOra(Guid strutturaId, CancellationToken cancellationToken)
    {
        var risultato = await invioService.InviaOraAsync(currentUser, strutturaId, cancellationToken);
        return Ok(new RisultatoInvioPayTouristDto(risultato.Inviate, risultato.TotalePrenotazioni, risultato.Errori, risultato.Messaggio));
    }

    /// <summary>Fallback: esporta come JSON le prenotazioni pronte per una struttura PayTourist senza inviarle né marcarle come inviate (stesso pattern "on-demand, mai persistito" di PDF/XML fattura e schedine Alloggiati Web).</summary>
    [HttpGet("strutture/{payTouristStrutturaId:guid}/export")]
    public async Task<IActionResult> Esporta(Guid strutturaId, Guid payTouristStrutturaId, CancellationToken cancellationToken)
    {
        var testo = await invioService.EsportaAsync(currentUser, strutturaId, payTouristStrutturaId, cancellationToken);
        return File(Encoding.UTF8.GetBytes(testo), "application/json", $"paytourist-{DateTime.UtcNow:yyyyMMdd}.json");
    }
}
