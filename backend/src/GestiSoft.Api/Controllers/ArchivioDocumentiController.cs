using GestiSoft.Application.Auth;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Contracts.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Archivio dei documenti fiscali delle strutture eliminate: solo Super Admin, ogni download nel Log.</summary>
[ApiController]
[Route("super-admin/archivio-documenti")]
[Authorize]
public class ArchivioDocumentiController(ArchivioDocumentiService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista([FromQuery] Guid? clienteId, CancellationToken cancellationToken)
    {
        var documenti = await service.ListaAsync(currentUser, clienteId, cancellationToken);
        return Ok(documenti.Select(d => new DocumentoArchiviatoDto(
            d.Id, d.ClienteId, d.ClienteRagioneSociale, d.StrutturaNome, d.StrutturaPartitaIva, d.Tipo, d.Numero, d.Data,
            d.Controparte, d.Importo, d.Pdf is not null, d.File is not null, d.ArchiviatoAtUtc, d.Scadenza)));
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var (nome, contenuto) = await service.ScaricaAsync(currentUser, id, pdf: true, cancellationToken);
        return File(contenuto, "application/pdf", nome);
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> FileElettronico(Guid id, CancellationToken cancellationToken)
    {
        var (nome, contenuto) = await service.ScaricaAsync(currentUser, id, pdf: false, cancellationToken);
        return File(contenuto, "application/octet-stream", nome);
    }

    /// <summary>Tutti i documenti di un Cliente in uno zip, per restituirglieli.</summary>
    [HttpGet("clienti/{clienteId:guid}/zip")]
    public async Task<IActionResult> Zip(Guid clienteId, CancellationToken cancellationToken)
    {
        var (nome, contenuto) = await service.ZipClienteAsync(currentUser, clienteId, cancellationToken);
        return File(contenuto, "application/zip", nome);
    }
}
