using GestiSoft.Application.Auth;
using GestiSoft.Application.Clienti;
using GestiSoft.Application.Utenti;
using GestiSoft.Contracts.Clienti;
using GestiSoft.Contracts.Utenti;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("clienti")]
[Authorize]
public class ClienteController(ClienteService service, UtenteManagementService utentiService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("{clienteId:guid}/utenti")]
    public async Task<IActionResult> ListaUtenti(Guid clienteId, CancellationToken cancellationToken)
    {
        var utenti = await utentiService.ListaUtentiClienteAsync(currentUser, clienteId, cancellationToken);
        return Ok(utenti.Select(u => new UtenteDto(u.Id, u.Email, u.Nome, u.Cognome, u.IsSuperAdmin, u.ClienteId, u.Attivo)));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var clienti = await service.ListAsync(currentUser, cancellationToken);
        return Ok(clienti.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var cliente = await service.GetByIdAsync(currentUser, id, cancellationToken);
        return Ok(ToDto(cliente));
    }

    [HttpPost]
    public async Task<IActionResult> Crea([FromBody] CreaClienteRequest request, CancellationToken cancellationToken)
    {
        var cliente = await service.CreaAsync(currentUser, request, cancellationToken);
        var dto = ToDto(cliente);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    private static ClienteDto ToDto(Domain.Entities.Cliente cliente) =>
        new(cliente.Id, cliente.RagioneSociale, cliente.PartitaIva, cliente.Attivo, cliente.CreatedAtUtc);
}
