using GestiSoft.Application.Auth;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Contracts.Prenotazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

public record CambiaStatoCameraRequest(StatoCamera NuovoStato);

[ApiController]
[Route("strutture/{strutturaId:guid}/prenotazioni")]
[Authorize]
public class PrenotazioniController(PrenotazioniService service, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>vista: "arrivi" (default), "in-corso", "storico" (richiede anno), "periodo" (richiede dataInizio/dataFine — usata dal booking board).</summary>
    [HttpGet]
    public async Task<IActionResult> Lista(
        Guid strutturaId,
        [FromQuery] string vista = "arrivi",
        [FromQuery] DateTime? daData = null,
        [FromQuery] int? anno = null,
        [FromQuery] DateTime? dataInizio = null,
        [FromQuery] DateTime? dataFine = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Prenotazione> prenotazioni = vista switch
        {
            "in-corso" => await service.ListaInCorsoAsync(currentUser, strutturaId, cancellationToken),
            "storico" => await service.ListaStoricoAsync(currentUser, strutturaId, anno ?? DateTime.UtcNow.Year, cancellationToken),
            "periodo" => await service.ListaPeriodoAsync(currentUser, strutturaId, dataInizio ?? DateTime.UtcNow.Date, dataFine ?? (dataInizio ?? DateTime.UtcNow.Date).AddDays(14), cancellationToken),
            _ => await service.ListaInArrivoAsync(currentUser, strutturaId, daData, cancellationToken),
        };

        return Ok(prenotazioni.Select(ToDto));
    }

    /// <summary>Agenzie (canali) distinte realmente usate dalla struttura — per il filtro del Calendario.</summary>
    [HttpGet("agenzie")]
    public async Task<IActionResult> Agenzie(Guid strutturaId, CancellationToken cancellationToken)
    {
        var agenzie = await service.ListaAgenzieAsync(currentUser, strutturaId, cancellationToken);
        return Ok(agenzie);
    }

    [HttpGet("{prenotazioneId:guid}")]
    public async Task<IActionResult> Get(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var prenotazione = await service.GetAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
        return Ok(ToDto(prenotazione));
    }

    /// <summary>Controllo live di sovrapposizione mostrato nel form prima del salvataggio (il controllo autorevole resta quello su Crea/Aggiorna).</summary>
    [HttpGet("verifica-disponibilita")]
    public async Task<IActionResult> VerificaDisponibilita(
        Guid strutturaId,
        [FromQuery] Guid cameraId,
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut,
        [FromQuery] Guid? escludiPrenotazioneId,
        CancellationToken cancellationToken)
    {
        var conflitto = await service.VerificaDisponibilitaAsync(currentUser, strutturaId, cameraId, checkIn, checkOut, escludiPrenotazioneId, cancellationToken);
        return Ok(new DisponibilitaCameraDto(
            conflitto is null,
            conflitto?.NumeroPrenotazione,
            conflitto?.Ospite?.Nome,
            conflitto?.Ospite?.Cognome,
            conflitto?.CheckIn,
            conflitto?.CheckOut));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] CreaPrenotazioneRequest request, CancellationToken cancellationToken)
    {
        var prenotazione = await service.CreaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(prenotazione));
    }

    [HttpPut("{prenotazioneId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid prenotazioneId, [FromBody] AggiornaPrenotazioneRequest request, CancellationToken cancellationToken)
    {
        var prenotazione = await service.AggiornaAsync(currentUser, strutturaId, prenotazioneId, request, cancellationToken);
        return Ok(ToDto(prenotazione));
    }

    [HttpPost("{prenotazioneId:guid}/annulla")]
    public async Task<IActionResult> Annulla(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var prenotazione = await service.AnnullaAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
        return Ok(ToDto(prenotazione));
    }

    [HttpPost("{prenotazioneId:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var prenotazione = await service.CheckInAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
        return Ok(ToDto(prenotazione));
    }

    [HttpPost("{prenotazioneId:guid}/check-out")]
    public async Task<IActionResult> CheckOut(Guid strutturaId, Guid prenotazioneId, [FromBody] CheckOutRequest request, CancellationToken cancellationToken)
    {
        var prenotazione = await service.CheckOutAsync(currentUser, strutturaId, prenotazioneId, request, cancellationToken);
        return Ok(ToDto(prenotazione));
    }

    [HttpPut("{prenotazioneId:guid}/stato-camera")]
    public async Task<IActionResult> CambiaStatoCamera(Guid strutturaId, Guid prenotazioneId, [FromBody] CambiaStatoCameraRequest request, CancellationToken cancellationToken)
    {
        await service.CambiaStatoCameraManualeAsync(currentUser, strutturaId, prenotazioneId, request.NuovoStato, cancellationToken);
        return NoContent();
    }

    private static PrenotazioneDto ToDto(Prenotazione p) => new(
        p.Id, p.StrutturaId, p.CameraId, p.Camera?.Nome, p.TipologiaId, p.Tipologia?.TipologiaCamera, p.Agenzia, p.NumeroPrenotazione,
        p.ImportoPrenotazione, p.ImportoPagato, p.ImportoTotale, p.CheckIn, p.CheckOut,
        p.NumeroOspiti, p.StatePolice, p.PMS, p.PayTourist, p.Anno, p.TotalTax, p.StatoPrenotazione,
        p.TassaSoggiornoAttiva, p.SpesePuliziaAttiva, p.AnimaliAttiva, p.CauzioneAttiva, p.Ospite?.Nome, p.Ospite?.Cognome);
}
