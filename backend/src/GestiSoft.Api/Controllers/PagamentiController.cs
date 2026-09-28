using GestiSoft.Application.Auth;
using GestiSoft.Application.Pagamenti;
using GestiSoft.Contracts.Pagamenti;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Registro dei pagamenti di una prenotazione: acconti, caparre, saldi e rimborsi.</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/prenotazioni/{prenotazioneId:guid}/pagamenti")]
[Authorize]
public class PagamentiController(PagamentiService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var righe = await service.ListaAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
        return Ok(righe.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Registra(Guid strutturaId, Guid prenotazioneId, [FromBody] SalvaPagamentoRequest request, CancellationToken cancellationToken)
    {
        var pagamento = await service.RegistraAsync(currentUser, strutturaId, prenotazioneId, request, cancellationToken);
        return Ok(ToDto(pagamento));
    }

    [HttpPut("{pagamentoId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid prenotazioneId, Guid pagamentoId, [FromBody] SalvaPagamentoRequest request, CancellationToken cancellationToken)
    {
        var pagamento = await service.AggiornaAsync(currentUser, strutturaId, prenotazioneId, pagamentoId, request, cancellationToken);
        return Ok(ToDto(pagamento));
    }

    [HttpDelete("{pagamentoId:guid}")]
    public async Task<IActionResult> Elimina(Guid strutturaId, Guid prenotazioneId, Guid pagamentoId, CancellationToken cancellationToken)
    {
        await service.EliminaAsync(currentUser, strutturaId, prenotazioneId, pagamentoId, cancellationToken);
        return NoContent();
    }

    private static PagamentoPrenotazioneDto ToDto(PagamentoPrenotazione p) =>
        new(p.Id, p.Data, p.Importo, p.Tipo, p.Metodo, p.Nota, p.RegistratoDa, p.CreatedAtUtc);
}
