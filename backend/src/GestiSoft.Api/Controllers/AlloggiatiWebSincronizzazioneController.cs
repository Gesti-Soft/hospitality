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
    /// <summary>Elenco schedine dell'anno indicato (default anno corrente) da inviare/già inviate — per la schermata operativa.</summary>
    [HttpGet("schedine")]
    public async Task<IActionResult> Lista(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var schedine = await invioService.ListSchedineAsync(currentUser, strutturaId, anno ?? DateTime.UtcNow.Year, cancellationToken);
        return Ok(schedine.Select(s => new SchedinaAlloggiatiWebDto(s.OspiteId, s.PrenotazioneId, s.NomeOspite, s.Camera, s.CheckIn, s.CheckOut, s.Inviata)));
    }

    /// <summary>Anni con almeno una prenotazione — per non proporre nel selettore Anno anni sicuramente vuoti.</summary>
    [HttpGet("schedine/anni")]
    public async Task<IActionResult> Anni(Guid strutturaId, CancellationToken cancellationToken)
    {
        var anni = await invioService.ListaAnniAsync(currentUser, strutturaId, cancellationToken);
        return Ok(anni);
    }

    [HttpPost("schedine/invia")]
    public async Task<IActionResult> InviaOra(Guid strutturaId, CancellationToken cancellationToken)
    {
        var risultato = await invioService.InviaOraAsync(currentUser, strutturaId, cancellationToken);
        return Ok(new RisultatoInvioAlloggiatiWebDto(risultato.Inviate, risultato.TotaleSchedine, risultato.Errori, risultato.Messaggio));
    }

    /// <summary>Fallback: esporta come file di testo le schedine ancora da inviare nell'anno indicato (stessa fonte della lista mostrata a schermo) senza inviarle né marcarle come inviate (stesso pattern "on-demand, mai persistito" di PDF/XML fattura in Fase 4).</summary>
    [HttpGet("schedine/export")]
    public async Task<IActionResult> Esporta(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var testo = await invioService.EsportaAsync(currentUser, strutturaId, anno ?? DateTime.UtcNow.Year, cancellationToken);
        return File(Encoding.UTF8.GetBytes(testo), "text/plain", $"schedine-alloggiati-web-{DateTime.UtcNow:yyyyMMdd}.txt");
    }

    /// <summary>Esporta come file di testo UNA sola schedina, oltre al bulk.</summary>
    [HttpGet("schedine/{ospiteId:guid}/export")]
    public async Task<IActionResult> EsportaSingola(Guid strutturaId, Guid ospiteId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var testo = await invioService.EsportaSingolaAsync(currentUser, strutturaId, ospiteId, anno ?? DateTime.UtcNow.Year, cancellationToken);
        return File(Encoding.UTF8.GetBytes(testo), "text/plain", $"schedina-alloggiati-web-{DateTime.UtcNow:yyyyMMdd}.txt");
    }
}
