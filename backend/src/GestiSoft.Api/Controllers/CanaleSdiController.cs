using GestiSoft.Application.Auth;
using GestiSoft.Application.Fatturazione.Sdi;
using GestiSoft.Contracts.SuperAdmin;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Pannello "Canale SdI" del Super Admin: quarantena, ricevute non abbinate, collaudo.</summary>
[ApiController]
[Route("super-admin/canale-sdi")]
[Authorize]
public class CanaleSdiController(CanaleSdiAdminService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Stato(CancellationToken cancellationToken)
    {
        var stato = await service.StatoAsync(currentUser, cancellationToken);
        return Ok(new CanaleSdiDto(
            stato.CodiceDestinatario,
            stato.CodiceFiscaleTrasmittente,
            stato.Collaudo,
            stato.Quarantena.Select(ToDto).ToList(),
            stato.RicevuteNonAbbinate.Select(r => new RicevutaSdiDto(r.Id, r.Tipo, r.IdentificativoSdi, r.NomeFileFattura, r.Prova, r.Descrizione, r.RicevutaAtUtc)).ToList(),
            stato.RicezioniDiCollaudo.Select(ToDto).ToList()));
    }

    [HttpPost("file-ricevuti/{fileRicevutoId:guid}/attribuisci")]
    public async Task<IActionResult> Attribuisci(Guid fileRicevutoId, [FromBody] AttribuisciFileRicevutoRequest request, CancellationToken cancellationToken)
    {
        await service.AttribuisciAsync(currentUser, fileRicevutoId, request.StrutturaId, cancellationToken);
        return NoContent();
    }

    [HttpGet("file-ricevuti/{fileRicevutoId:guid}/file")]
    public async Task<IActionResult> ScaricaFileRicevuto(Guid fileRicevutoId, CancellationToken cancellationToken)
    {
        var (nomeFile, file) = await service.FileRicevutoAsync(currentUser, fileRicevutoId, cancellationToken);
        return File(file, "application/octet-stream", nomeFile);
    }

    [HttpGet("ricevute/{ricevutaId:guid}/file")]
    public async Task<IActionResult> ScaricaRicevuta(Guid ricevutaId, CancellationToken cancellationToken)
    {
        var (nomeFile, file) = await service.RicevutaAsync(currentUser, ricevutaId, cancellationToken);
        return File(file, "application/xml", nomeFile);
    }

    private static FileRicevutoSdiDto ToDto(FileRicevutoSdi f) =>
        new(f.Id, f.IdentificativoSdi, f.NomeFile, f.Fornitore, f.IdentificativoCessionario, f.Esito, f.Messaggio, f.RicevutoAtUtc);
}
