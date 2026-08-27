using System.Text;
using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Application.Auth;
using GestiSoft.Contracts.AlloggiatiWeb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Invio manuale e export delle schedine Alloggiati Web — la stessa azione eseguita in automatico dal job Quartz giornaliero nel Worker, richiamabile a mano da UI senza aspettare l'orario configurato.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/alloggiati-web")]
[Authorize]
public class AlloggiatiWebSincronizzazioneController(AlloggiatiWebInvioService invioService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("schedine/invia")]
    public async Task<IActionResult> InviaOra(Guid strutturaId, CancellationToken cancellationToken)
    {
        var risultato = await invioService.InviaOraAsync(currentUser, strutturaId, cancellationToken);
        return Ok(new RisultatoInvioAlloggiatiWebDto(risultato.Inviate, risultato.TotaleSchedine, risultato.Errori, risultato.Messaggio));
    }

    /// <summary>Fallback: esporta come file di testo le schedine del giorno senza inviarle né marcarle come inviate (stesso pattern "on-demand, mai persistito" di PDF/XML fattura in Fase 4).</summary>
    [HttpGet("schedine/export")]
    public async Task<IActionResult> Esporta(Guid strutturaId, CancellationToken cancellationToken)
    {
        var testo = await invioService.EsportaAsync(currentUser, strutturaId, cancellationToken);
        return File(Encoding.UTF8.GetBytes(testo), "text/plain", $"schedine-alloggiati-web-{DateTime.UtcNow:yyyyMMdd}.txt");
    }
}
