using GestiSoft.Application.Auth;
using GestiSoft.Application.Ospiti;
using GestiSoft.Contracts.Ospiti;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/prenotazioni/{prenotazioneId:guid}/ospiti")]
[Authorize]
public class OspitiController(OspitiService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var ospite = await service.GetSchedaAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
        return ospite is null ? NotFound() : Ok(ToDto(ospite));
    }

    [HttpPut]
    public async Task<IActionResult> Salva(Guid strutturaId, Guid prenotazioneId, [FromBody] SalvaSchedaOspitiRequest request, CancellationToken cancellationToken)
    {
        var ospite = await service.SalvaSchedaAsync(currentUser, strutturaId, prenotazioneId, request, cancellationToken);
        return Ok(ToDto(ospite));
    }

    private static OspiteDto ToDto(Ospite o) => new(
        o.Id, o.StrutturaId, o.PrenotazioneId, o.TipoOspite, o.Permanenza, o.DataNascita, o.Sesso,
        o.Cognome, o.Nome, o.Cittadinanza, o.LuogoNascita, o.StatoNascita, o.LuogoResidenza,
        o.Email, o.Documento, o.NumeroDocumento, o.RilascioDocumento, o.EsenteDaTassa,
        o.Membri.Select(ToDto).ToList());

    private static OspiteRigaDto ToDto(OspiteRiga r) => new(
        r.Id, r.CameraId, r.Permanenza, r.DataNascita, r.Sesso, r.Cognome, r.Nome, r.Cittadinanza,
        r.LuogoNascita, r.StatoNascita, r.LuogoResidenza, r.PostoLetto, r.EsenteDaTassa);
}
