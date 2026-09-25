using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Contracts.Camere;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/prezzi-camera")]
[Authorize]
public class PrezziCameraController(PrezziCameraService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var prezzi = await service.ListaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(prezzi.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Imposta(Guid strutturaId, [FromBody] ImpostaPrezzoRequest request, CancellationToken cancellationToken)
    {
        var risultato = await service.ImpostaPrezzoAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(risultato.Prezzo) with { AvvisoOta = risultato.AvvisoOta });
    }

    [HttpDelete("{prezzoId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid prezzoId, CancellationToken cancellationToken)
    {
        var avvisoOta = await service.EliminaAsync(currentUser, strutturaId, prezzoId, cancellationToken);
        return Ok(new PeriodoPrezzoEliminatoDto(avvisoOta));
    }

    [HttpGet("preventivo")]
    public async Task<IActionResult> Preventivo(
        Guid strutturaId,
        [FromQuery] Guid cameraId,
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut,
        [FromQuery] int numeroOspiti,
        [FromQuery] int[]? etaBambini,
        [FromQuery] bool spesePulizia = true,
        [FromQuery] bool animali = false,
        [FromQuery] bool cauzione = true,
        [FromQuery] TipoTrattamento? trattamento = null,
        [FromQuery] Guid? prenotazioneId = null,
        CancellationToken cancellationToken = default)
    {
        var preventivo = await service.CalcolaPreventivoAsync(
            currentUser, strutturaId, cameraId, checkIn, checkOut, numeroOspiti, etaBambini ?? [], spesePulizia, animali, cauzione, trattamento, prenotazioneId, cancellationToken);
        return Ok(new PreventivoDto(preventivo.Notti, preventivo.Totale));
    }

    /// <summary>Fasce d'età del supplemento per persona in più della tipologia (vedi FasciaEtaSupplemento).</summary>
    [HttpGet("~/strutture/{strutturaId:guid}/tipologie-camera/{tipologiaId:guid}/fasce-eta")]
    public async Task<IActionResult> FasceEta(Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        var fasce = await service.ListaFasceEtaAsync(currentUser, strutturaId, tipologiaId, cancellationToken);
        return Ok(fasce.Select(f => new FasciaEtaSupplementoDto(f.EtaMin, f.EtaMax, f.ImportoPerNotte, f.TipoImporto)));
    }

    [HttpPut("~/strutture/{strutturaId:guid}/tipologie-camera/{tipologiaId:guid}/fasce-eta")]
    public async Task<IActionResult> SalvaFasceEta(Guid strutturaId, Guid tipologiaId, [FromBody] List<FasciaEtaSupplementoDto> request, CancellationToken cancellationToken)
    {
        var fasce = await service.SalvaFasceEtaAsync(currentUser, strutturaId, tipologiaId, request, cancellationToken);
        return Ok(fasce.Select(f => new FasciaEtaSupplementoDto(f.EtaMin, f.EtaMax, f.ImportoPerNotte, f.TipoImporto)));
    }

    /// <summary>Prezzo per numero di ospiti della tipologia: riduzione per ospite in meno e unità del supplemento (vedi SettingTipologia).</summary>
    [HttpPut("~/strutture/{strutturaId:guid}/tipologie-camera/{tipologiaId:guid}/prezzi-occupazione")]
    public async Task<IActionResult> SalvaPrezziOccupazione(Guid strutturaId, Guid tipologiaId, [FromBody] PrezziOccupazioneDto request, CancellationToken cancellationToken)
    {
        var t = await service.SalvaPrezziOccupazioneAsync(currentUser, strutturaId, tipologiaId, request, cancellationToken);
        return Ok(new PrezziOccupazioneDto(t.RiduzioneOspiteInMeno, t.TipoRiduzioneOspiteInMeno, t.TipoImplemento));
    }

    private static PrezzoCameraDto ToDto(GestionePrezzo p) => new(
        p.Id, p.StrutturaId, p.CameraId, p.TipologiaId, p.DataInizio, p.DataFine, p.PrezzoPerNotte);
}
