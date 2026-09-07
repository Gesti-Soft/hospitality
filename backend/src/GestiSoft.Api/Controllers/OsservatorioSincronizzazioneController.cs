using GestiSoft.Application.Auth;
using GestiSoft.Application.Osservatorio;
using GestiSoft.Contracts.Osservatorio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Invio manuale verso l'Osservatorio Turistico per un singolo appartamento — la stessa azione eseguita in automatico dal job Quartz giornaliero nel Worker, richiamabile a mano da UI senza aspettare l'orario configurato.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/osservatorio/appartamenti/{appartamentoId:guid}")]
[Authorize]
public class OsservatorioSincronizzazioneController(OsservatorioInvioService invioService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Elenco arrivi/partenze dell'anno indicato (default anno corrente) da inviare/già inviati — per la schermata operativa.</summary>
    [HttpGet("schedine")]
    public async Task<IActionResult> Lista(Guid strutturaId, Guid appartamentoId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var schedine = await invioService.ListSchedineAsync(currentUser, strutturaId, appartamentoId, anno ?? DateTime.UtcNow.Year, cancellationToken);
        return Ok(schedine.Select(s => new SchedinaOsservatorioDto(s.OspiteId, s.PrenotazioneId, s.NomeOspite, s.Camera, s.CheckIn, s.CheckOut, s.ArrivoInviato, s.PartenzaInviata)));
    }

    /// <summary>
    /// Anni con almeno una prenotazione — per non proporre nel selettore Anno anni sicuramente vuoti.
    /// A livello di Struttura, non di singolo appartamento (il dato è lo stesso per tutti gli
    /// appartamenti): rotta assoluta (<c>~/</c>) per uscire dal segmento {appartamentoId} richiesto
    /// dalla rotta di base di questo controller.
    /// </summary>
    [HttpGet("~/strutture/{strutturaId:guid}/osservatorio/anni")]
    public async Task<IActionResult> Anni(Guid strutturaId, CancellationToken cancellationToken)
    {
        var anni = await invioService.ListaAnniAsync(currentUser, strutturaId, cancellationToken);
        return Ok(anni);
    }

    [HttpPost("invia")]
    public async Task<IActionResult> InviaOra(Guid strutturaId, Guid appartamentoId, CancellationToken cancellationToken)
    {
        var risultato = await invioService.InviaOraAsync(currentUser, strutturaId, appartamentoId, cancellationToken);
        return Ok(new RisultatoInvioOsservatorioDto(risultato.ArriviInviati, risultato.CheckoutInviati, risultato.GiorniChiusi, risultato.Messaggio));
    }
}
