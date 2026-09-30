using GestiSoft.Application.Auth;
using GestiSoft.Application.Servizi;
using GestiSoft.Contracts.Servizi;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Servizi extra della struttura (escursioni, parcheggio…) e quelli venduti con una prenotazione.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/servizi")]
[Authorize]
public class ServiziController(ServiziService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var servizi = await service.ListaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(servizi.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] SalvaServizioRequest request, CancellationToken cancellationToken)
    {
        var servizio = await service.CreaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(servizio));
    }

    [HttpPut("{servizioId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid servizioId, [FromBody] SalvaServizioRequest request, CancellationToken cancellationToken)
    {
        var servizio = await service.AggiornaAsync(currentUser, strutturaId, servizioId, request, cancellationToken);
        return Ok(ToDto(servizio));
    }

    [HttpDelete("{servizioId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid servizioId, CancellationToken cancellationToken)
    {
        await service.EliminaAsync(currentUser, strutturaId, servizioId, cancellationToken);
        return NoContent();
    }

    [HttpGet("~/strutture/{strutturaId:guid}/prenotazioni/{prenotazioneId:guid}/servizi")]
    public async Task<IActionResult> DellaPrenotazione(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var righe = await service.ListaDellaPrenotazioneAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
        return Ok(righe.Select(r => new ServizioPrenotazioneDto(
            r.Id, r.ServizioId, r.Nome, r.Modalita, r.PrezzoUnitario, r.Quantita, r.Dal, r.Al, r.Origine, r.AggiuntoDa, r.CreatedAtUtc)));
    }

    /// <summary>Addebito rapido dalla reception: la riga si aggiunge e il suo importo va sul totale.</summary>
    [HttpPost("~/strutture/{strutturaId:guid}/prenotazioni/{prenotazioneId:guid}/servizi")]
    public async Task<IActionResult> Addebita(Guid strutturaId, Guid prenotazioneId, [FromBody] ServizioPrenotazioneRichiesta request, CancellationToken cancellationToken)
    {
        var r = await service.AddebitaAsync(currentUser, strutturaId, prenotazioneId, request, cancellationToken);
        return Ok(new ServizioPrenotazioneDto(r.Id, r.ServizioId, r.Nome, r.Modalita, r.PrezzoUnitario, r.Quantita, r.Dal, r.Al, r.Origine, r.AggiuntoDa, r.CreatedAtUtc));
    }

    private static ServizioStrutturaDto ToDto(ServizioStruttura s) => new(s.Id, s.Nome, s.Prezzo, s.Modalita, s.Attivo, s.AliquotaIva, s.Natura, s.Codice);
}
