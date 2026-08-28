using GestiSoft.Application.Riferimenti;
using GestiSoft.Contracts.Riferimenti;
using GestiSoft.Domain.Entities.Riferimenti;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>
/// Tabelle di riferimento condivise (Comuni/Stati/Documenti d'identità/TipoAlloggiato) usate per
/// popolare i menu a tendina della scheda ospiti (Alloggiati Web) — dati globali di sola lettura,
/// non legati a una Struttura: nessuna rotta annidata sotto strutture/{id}.
/// </summary>
[ApiController]
[Route("riferimenti")]
[Authorize]
public class RiferimentiController(RiferimentiService service) : ControllerBase
{
    [HttpGet("stati")]
    public async Task<IActionResult> ListaStati(CancellationToken cancellationToken)
    {
        var stati = await service.ListaStatiAsync(cancellationToken);
        return Ok(stati.Select(ToDto));
    }

    [HttpGet("comuni")]
    public async Task<IActionResult> CercaComuni([FromQuery] string? ricerca, CancellationToken cancellationToken)
    {
        var comuni = await service.CercaComuniAsync(ricerca, cancellationToken);
        return Ok(comuni.Select(ToDto));
    }

    [HttpGet("documenti")]
    public async Task<IActionResult> ListaDocumenti(CancellationToken cancellationToken)
    {
        var documenti = await service.ListaDocumentiAsync(cancellationToken);
        return Ok(documenti.Select(ToDto));
    }

    [HttpGet("tipi-alloggiato")]
    public async Task<IActionResult> ListaTipiAlloggiato(CancellationToken cancellationToken)
    {
        var tipi = await service.ListaTipiAlloggiatoAsync(cancellationToken);
        return Ok(tipi.Select(ToDto));
    }

    private static StatoDto ToDto(Stato s) => new(s.Id, s.Codice, s.Descrizione, s.NomeInglese, s.Acronimo);

    private static ComuneDto ToDto(Comune c) => new(c.Id, c.Codice, c.Descrizione, c.Provincia, c.CodiceBelfiore, c.Cap);

    private static DocumentoDto ToDto(Documento d) => new(d.Id, d.Codice, d.Descrizione, d.TypeId);

    private static TipoAlloggiatoDto ToDto(TipoAlloggiato t) => new(t.Id, t.Codice, t.Descrizione);
}
