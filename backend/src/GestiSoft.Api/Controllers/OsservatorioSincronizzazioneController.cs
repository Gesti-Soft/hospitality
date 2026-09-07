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

    [HttpPost("invia")]
    public async Task<IActionResult> InviaOra(Guid strutturaId, Guid appartamentoId, CancellationToken cancellationToken)
    {
        var risultato = await invioService.InviaOraAsync(currentUser, strutturaId, appartamentoId, cancellationToken);
        return Ok(new RisultatoInvioOsservatorioDto(risultato.ArriviInviati, risultato.CheckoutInviati, risultato.GiorniChiusi, risultato.Messaggio));
    }
}
