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

    /// <summary>Tutti i propri permessi su questa struttura (non l'intero elenco) — usato dal frontend per decidere quali pagine/voci di menu mostrare, senza dover scaricare il roster completo.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Mio(Guid strutturaId, CancellationToken cancellationToken)
    {
        var p = await service.GetMioPermessoAsync(currentUser, strutturaId, cancellationToken);
        return Ok(new MioPermessoStrutturaDto(
            p.BookingRead, p.BookingWrite, p.ReservationRead, p.ReservationWrite,
            p.StatePoliceRead, p.StatePoliceWrite, p.StatePoliceSettings,
            p.SettingAgency, p.SettingUser, p.SettingRoomRead, p.SettingRoomWrite, p.RoomStatusUpdate,
            p.FinanceRead, p.FinanceWrite, p.RestaurantRead, p.RestaurantWrite));
    }
}

public record MioPermessoStrutturaDto(
    bool BookingRead,
    bool BookingWrite,
    bool ReservationRead,
    bool ReservationWrite,
    bool StatePoliceRead,
    bool StatePoliceWrite,
    bool StatePoliceSettings,
    bool SettingAgency,
    bool SettingUser,
    bool SettingRoomRead,
    bool SettingRoomWrite,
    bool RoomStatusUpdate,
    bool FinanceRead,
    bool FinanceWrite,
    bool RestaurantRead,
    bool RestaurantWrite);
