using GestiSoft.Application.Auth;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Contracts.Fatturazione;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/dati-cliente")]
[Authorize]
public class DatiClienteController(DatiClienteService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var clienti = await service.ListaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(clienti.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Crea(Guid strutturaId, [FromBody] CreaDatiClienteRequest request, CancellationToken cancellationToken)
    {
        var cliente = await service.CreaAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(cliente));
    }

    [HttpPut("{clienteId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid clienteId, [FromBody] CreaDatiClienteRequest request, CancellationToken cancellationToken)
    {
        var cliente = await service.AggiornaAsync(currentUser, strutturaId, clienteId, request, cancellationToken);
        return Ok(ToDto(cliente));
    }

    private static DatiClienteDto ToDto(DatiCliente c) => new(
        c.Id, c.StrutturaId, c.Iso2, c.PIva, c.CodiceFiscale, c.Denominazione, c.Nome, c.Cognome,
        c.Indirizzo, c.NCivico, c.Cap, c.LuogoResidenza, c.Provincia, c.Cittadinanza,
        c.CodiceDestinatario, c.Pec, c.CustomerKey);
}
