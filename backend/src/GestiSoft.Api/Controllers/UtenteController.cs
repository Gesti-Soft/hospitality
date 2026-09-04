using GestiSoft.Application.Auth;
using GestiSoft.Application.Utenti;
using GestiSoft.Contracts.Utenti;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("utenti")]
[Authorize]
public class UtenteController(UtenteManagementService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Crea([FromBody] CreaUtenteRequest request, CancellationToken cancellationToken)
    {
        var utente = await service.CreaAsync(currentUser, request, cancellationToken);
        var dto = new UtenteDto(utente.Id, utente.Email, utente.Nome, utente.Cognome, utente.IsSuperAdmin, utente.ClienteId, utente.Attivo, utente.IsClienteAccount);
        return Ok(dto);
    }

    [HttpPut("{utenteId:guid}/strutture/{strutturaId:guid}/ruolo")]
    public async Task<IActionResult> AssegnaRuolo(
        Guid utenteId,
        Guid strutturaId,
        [FromBody] AssegnaRuoloRequest request,
        CancellationToken cancellationToken)
    {
        var a = await service.AssegnaRuoloAsync(currentUser, utenteId, strutturaId, request, cancellationToken);
        var dto = new UtenteStrutturaDto(
            a.Id, a.UtenteId, a.StrutturaId, a.Ruolo,
            a.BookingRead, a.BookingWrite, a.ReservationRead, a.ReservationWrite,
            a.StatePoliceRead, a.StatePoliceWrite, a.StatePoliceSettings,
            a.SettingAgency, a.SettingUser, a.SettingRoomRead, a.SettingRoomWrite, a.RoomStatusUpdate,
            a.FinanceRead, a.FinanceWrite, a.RestaurantRead, a.RestaurantWrite);
        return Ok(dto);
    }

    [HttpDelete("{utenteId:guid}/strutture/{strutturaId:guid}")]
    public async Task<IActionResult> RimuoviAssegnazione(Guid utenteId, Guid strutturaId, CancellationToken cancellationToken)
    {
        await service.RimuoviAssegnazioneAsync(currentUser, utenteId, strutturaId, cancellationToken);
        return NoContent();
    }

    [HttpPost("me/cambia-password")]
    public async Task<IActionResult> CambiaPassword([FromBody] CambiaPasswordRequest request, CancellationToken cancellationToken)
    {
        await service.CambiaPasswordAsync(currentUser, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{utenteId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid utenteId, [FromBody] AggiornaUtenteRequest request, CancellationToken cancellationToken)
    {
        var utente = await service.AggiornaAsync(currentUser, utenteId, request, cancellationToken);
        var dto = new UtenteDto(utente.Id, utente.Email, utente.Nome, utente.Cognome, utente.IsSuperAdmin, utente.ClienteId, utente.Attivo, utente.IsClienteAccount);
        return Ok(dto);
    }

    [HttpPost("{utenteId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid utenteId, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await service.ResetPasswordAsync(currentUser, utenteId, request, cancellationToken);
        return NoContent();
    }
}
