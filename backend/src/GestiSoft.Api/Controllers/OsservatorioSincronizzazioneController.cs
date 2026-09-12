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
    /// <summary>
    /// Elenco arrivi/partenze dell'anno indicato (default anno corrente) di TUTTA la Struttura, non
    /// del singolo appartamento: ogni riga porta con sé l'appartamento a cui appartiene, dedotto
    /// dalla tipologia della camera. Rotta assoluta (<c>~/</c>) per uscire dal segmento
    /// {appartamentoId} della rotta di base.
    /// </summary>
    [HttpGet("~/strutture/{strutturaId:guid}/osservatorio/schedine")]
    public async Task<IActionResult> Lista(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var schedine = await invioService.ListSchedineAsync(currentUser, strutturaId, anno ?? DateTime.UtcNow.Year, cancellationToken);
        return Ok(schedine.Select(s => new SchedinaOsservatorioDto(
            s.OspiteId, s.PrenotazioneId, s.NomeOspite, s.Camera, s.CheckIn, s.CheckOut, s.ArrivoInviato, s.PartenzaInviata,
            s.ChiusoFinoA, s.InTermine, s.AppartamentoId, s.AppartamentoNome)));
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

    /// <summary>
    /// Giornata da chiudere letta dal servizio Osservatorio per ogni appartamento — non dalla cache
    /// locale, che riflette solo gli invii partiti da qui. Sola lettura: non trasmette nulla.
    /// </summary>
    [HttpGet("~/strutture/{strutturaId:guid}/osservatorio/stato")]
    public async Task<IActionResult> Stato(Guid strutturaId, CancellationToken cancellationToken)
    {
        var stati = await invioService.LeggiStatoRemotoAsync(currentUser, strutturaId, cancellationToken);
        return Ok(stati.Select(s => new StatoAppartamentoOsservatorioDto(s.AppartamentoId, s.Nome, s.ChiusoFinoA, s.Errore)));
    }

    /// <summary>
    /// Invio manuale per tutti gli appartamenti della Struttura: la schermata non fa più scegliere
    /// un appartamento, quindi il pulsante "Invia ora" li processa tutti. Gli esiti dei singoli
    /// appartamenti sono sommati in uno solo, con i messaggi accorpati.
    /// </summary>
    [HttpPost("~/strutture/{strutturaId:guid}/osservatorio/invia")]
    public async Task<IActionResult> InviaOraTutti(Guid strutturaId, CancellationToken cancellationToken)
    {
        var risultati = await invioService.InviaOraTuttiAsync(currentUser, strutturaId, cancellationToken);
        var messaggi = risultati.Select(r => r.Messaggio).Where(m => !string.IsNullOrWhiteSpace(m)).ToList();

        return Ok(new RisultatoInvioOsservatorioDto(
            risultati.Sum(r => r.ArriviInviati),
            risultati.Sum(r => r.CheckoutInviati),
            risultati.Sum(r => r.GiorniChiusi),
            messaggi.Count == 0 ? null : string.Join(" ", messaggi)));
    }

    /// <summary>
    /// Invio del solo arrivo indicato (pulsante sulla riga), senza chiusura di giornata — come
    /// l'invio manuale dell'appartamento. L'appartamento non va indicato: viene dedotto dalla
    /// tipologia della camera dell'ospite, che è ciò che stabilisce dove va dichiarato.
    /// </summary>
    [HttpPost("~/strutture/{strutturaId:guid}/osservatorio/schedine/{ospiteId:guid}/invia")]
    public async Task<IActionResult> InviaSingola(Guid strutturaId, Guid ospiteId, CancellationToken cancellationToken)
    {
        var risultato = await invioService.InviaSingoloArrivoAsync(currentUser, strutturaId, ospiteId, cancellationToken);
        return Ok(new RisultatoInvioOsservatorioDto(risultato.ArriviInviati, risultato.CheckoutInviati, risultato.GiorniChiusi, risultato.Messaggio));
    }
}
