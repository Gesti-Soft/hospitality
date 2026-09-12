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
    /// <summary>Anni con almeno una prenotazione — per non proporre nel selettore Anno anni sicuramente vuoti.</summary>
    [HttpGet("anni")]
    public async Task<IActionResult> Anni(Guid strutturaId, CancellationToken cancellationToken)
    {
        var anni = await invioService.ListaAnniAsync(currentUser, strutturaId, cancellationToken);
        return Ok(anni);
    }

    [HttpPost("invia")]
    public async Task<IActionResult> InviaOra(Guid strutturaId, CancellationToken cancellationToken)
    {
        var risultato = await invioService.InviaOraAsync(currentUser, strutturaId, cancellationToken);
        return Ok(new RisultatoInvioPayTouristDto(risultato.Inviate, risultato.TotalePrenotazioni, risultato.Errori, risultato.Messaggio));
    }

    /// <summary>
    /// Invio di una singola prenotazione (per Ospite) a PayTourist, oltre al bulk "Invia ora tutte".
    /// La struttura PayTourist di destinazione non si indica: viene dedotta dalla tipologia della
    /// camera dell'ospite.
    /// </summary>
    [HttpPost("prenotazioni/{ospiteId:guid}/invia")]
    public async Task<IActionResult> InviaSingola(Guid strutturaId, Guid ospiteId, CancellationToken cancellationToken)
    {
        await invioService.InviaSingolaAsync(currentUser, strutturaId, ospiteId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Elenco prenotazioni dell'anno indicato (default anno corrente) di TUTTA la Struttura, non
    /// della singola struttura PayTourist: ogni riga porta con sé quella in cui va dichiarata,
    /// dedotta dalla tipologia della camera.
    /// </summary>
    [HttpGet("prenotazioni")]
    public async Task<IActionResult> Lista(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var prenotazioni = await invioService.ListPrenotazioniAsync(currentUser, strutturaId, anno ?? DateTime.UtcNow.Year, cancellationToken);
        return Ok(prenotazioni.Select(p => new PrenotazionePayTouristDto(
            p.OspiteId, p.PrenotazioneId, p.NomeOspite, p.Camera, p.CheckIn, p.CheckOut, p.Inviata,
            p.ScadenzaInvioUtc, p.InTermine, p.PayTouristStrutturaId, p.PayTouristStrutturaNome)));
    }

    /// <summary>Fallback: esporta come JSON le prenotazioni pronte per una struttura PayTourist senza inviarle né marcarle come inviate (stesso pattern "on-demand, mai persistito" di PDF/XML fattura e schedine Alloggiati Web).</summary>
    [HttpGet("strutture/{payTouristStrutturaId:guid}/export")]
    public async Task<IActionResult> Esporta(Guid strutturaId, Guid payTouristStrutturaId, CancellationToken cancellationToken)
    {
        var testo = await invioService.EsportaAsync(currentUser, strutturaId, payTouristStrutturaId, cancellationToken);
        return File(Encoding.UTF8.GetBytes(testo), "application/json", $"paytourist-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    /// <summary>Esporta in un solo file le prenotazioni pronte di tutte le strutture PayTourist configurate — la schermata non ne fa più scegliere una.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> EsportaTutte(Guid strutturaId, CancellationToken cancellationToken)
    {
        var testo = await invioService.EsportaTutteAsync(currentUser, strutturaId, cancellationToken);
        return File(Encoding.UTF8.GetBytes(testo), "application/json", $"paytourist-{DateTime.UtcNow:yyyyMMdd}.json");
    }
}
