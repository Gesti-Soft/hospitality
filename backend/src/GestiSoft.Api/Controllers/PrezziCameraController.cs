using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Contracts.Camere;
using GestiSoft.Contracts.Servizi;
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
        // Servizi extra come "idRiga:idServizio:quantità:dal:al" (idRiga e al "-" se mancano), uno per parametro.
        [FromQuery] string[]? servizi = null,
        CancellationToken cancellationToken = default)
    {
        var richieste = (servizi ?? []).Select(LeggiServizio).ToList();
        var preventivo = await service.CalcolaPreventivoAsync(
            currentUser, strutturaId, cameraId, checkIn, checkOut, numeroOspiti, etaBambini ?? [], spesePulizia, animali, cauzione, trattamento, prenotazioneId, richieste, cancellationToken);
        return Ok(new PreventivoDto(preventivo.Notti, preventivo.Totale));
    }

    private static ServizioPrenotazioneRichiesta LeggiServizio(string valore)
    {
        var parti = valore.Split(':');
        if (parti.Length == 5
            && (parti[0] == "-" || Guid.TryParse(parti[0], out _))
            && Guid.TryParse(parti[1], out var servizioId)
            && int.TryParse(parti[2], out var quantita)
            && DateOnly.TryParseExact(parti[3], "yyyy-MM-dd", out var dal)
            && (parti[4] == "-" || DateOnly.TryParseExact(parti[4], "yyyy-MM-dd", out _)))
        {
            Guid? rigaId = parti[0] == "-" ? null : Guid.Parse(parti[0]);
            DateOnly? al = parti[4] == "-" ? null : DateOnly.ParseExact(parti[4], "yyyy-MM-dd");
            return new ServizioPrenotazioneRichiesta(rigaId, servizioId, quantita, dal, al);
        }

        throw new ConflictException("Servizio del preventivo non valido.");
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
