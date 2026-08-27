using GestiSoft.Application.Auth;
using GestiSoft.Application.Utenti;
using GestiSoft.Contracts.Utenti;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>Elenco delle assegnazioni Utente/ruolo/permessi per una Struttura — usata dalla schermata Utenti del frontend (Fase 9).</summary>
[ApiController]
[Route("strutture/{strutturaId:guid}/utenti")]
[Authorize]
public class StrutturaUtentiController(UtenteManagementService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, CancellationToken cancellationToken)
    {
        var assegnazioni = await service.ListaAssegnazioniStrutturaAsync(currentUser, strutturaId, cancellationToken);
        return Ok(assegnazioni.Select(a => new AssegnazioneStrutturaDto(
            a.Id, a.UtenteId, a.Utente!.Email, a.Utente.Nome, a.Utente.Cognome, a.StrutturaId, a.Ruolo,
            a.BookingRead, a.BookingWrite, a.ReservationRead, a.ReservationWrite,
            a.StatePoliceRead, a.StatePoliceWrite, a.StatePoliceSettings,
            a.SettingAgency, a.SettingUser, a.SettingRoomRead, a.SettingRoomWrite, a.RoomStatusUpdate,
            a.FinanceRead, a.FinanceWrite, a.RestaurantRead, a.RestaurantWrite)));
    }
}
