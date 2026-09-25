using GestiSoft.Application.Auth;
using GestiSoft.Application.Pulizie;
using GestiSoft.Contracts.Pulizie;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Pulizia e cambio biancheria delle camere occupate, vedi PulizieSoggiornoService.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}")]
[Authorize]
public class PulizieSoggiornoController(PulizieSoggiornoService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("pulizie/soggiorni")]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var soggiorni = await service.ListaAsync(currentUser, strutturaId, cancellationToken);
        var oggi = PulizieSoggiornoService.Oggi();

        return Ok(soggiorni.Select(s =>
        {
            var p = s.Prenotazione;
            int? notti = p.CheckIn is { } a && p.CheckOut is { } d && d.Date > a.Date ? (d.Date - a.Date).Days : null;
            // "Notte 3 di 7": la notte che inizia oggi. Dopo la data di partenza (check-out dimenticato) resta l'ultima.
            int? notteCorrente = notti is { } n && p.CheckIn is { } arrivo ? Math.Clamp((oggi - arrivo.Date).Days + 1, 1, n) : null;

            return new SoggiornoPulizieDto(
                p.Id,
                p.Camera?.Nome,
                s.TipologiaNome,
                p.NumeroOspiti,
                p.CheckIn,
                p.CheckOut,
                notteCorrente,
                notti,
                new ServizioSoggiornoDto(s.Pulizia.Stato, s.Pulizia.IntervalloGiorni, s.Pulizia.Previsto, p.UltimaPuliziaSoggiorno),
                new ServizioSoggiornoDto(s.Biancheria.Stato, s.Biancheria.IntervalloGiorni, s.Biancheria.Previsto, p.UltimoCambioBiancheria));
        }));
    }

    [HttpPost("pulizie/soggiorni/{prenotazioneId:guid}/{servizio}")]
    public async Task<IActionResult> SegnaFatto(Guid strutturaId, Guid prenotazioneId, ServizioSoggiorno servizio, CancellationToken cancellationToken)
    {
        await service.SegnaFattoAsync(currentUser, strutturaId, prenotazioneId, servizio, cancellationToken);
        return NoContent();
    }

    [HttpPut("prenotazioni/{prenotazioneId:guid}/rinunce-servizi")]
    public async Task<IActionResult> AggiornaRinunce(Guid strutturaId, Guid prenotazioneId, [FromBody] RinunceServiziRequest request, CancellationToken cancellationToken)
    {
        var p = await service.AggiornaRinunceAsync(currentUser, strutturaId, prenotazioneId, request, cancellationToken);
        return Ok(new RinunceServiziRequest(p.RinunciaPulizia, p.RinunciaBiancheria));
    }

    [HttpPut("tipologie-camera/{tipologiaId:guid}/pulizie")]
    public async Task<IActionResult> AggiornaTipologia(Guid strutturaId, Guid tipologiaId, [FromBody] ImpostazioniPulizieTipologiaRequest request, CancellationToken cancellationToken)
    {
        var t = await service.AggiornaTipologiaAsync(currentUser, strutturaId, tipologiaId, request, cancellationToken);
        return Ok(new ImpostazioniPulizieTipologiaRequest(t.IntervalloPuliziaGiorni, t.IntervalloBiancheriaGiorni));
    }
}
