using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace GestiSoft.Application.Utenti;

public record CreaUtenteRequest(string Email, string Password, string? Nome, string? Cognome, bool IsSuperAdmin, Guid? ClienteId);

public record AssegnaRuoloRequest(
    RuoloUtente Ruolo,
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

public record CambiaPasswordRequest(string PasswordAttuale, string PasswordNuova);

public class UtenteManagementService(
    IUtenteRepository utenti,
    IUtenteStrutturaRepository utentiStrutture,
    IStrutturaRepository strutture,
    IPasswordHasher<Utente> passwordHasher)
{
    public async Task<Utente> CreaAsync(ICurrentUser currentUser, CreaUtenteRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin && request.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo un Super Admin può creare un altro Super Admin.");
        }

        Guid? clienteId;
        if (request.IsSuperAdmin)
        {
            clienteId = null;
        }
        else if (currentUser.IsSuperAdmin)
        {
            clienteId = request.ClienteId ?? throw new ConflictException("Specificare il Cliente dell'utente.");
        }
        else
        {
            // Un Cliente crea utenti solo per sé stesso, a prescindere da cosa passa in request.
            clienteId = currentUser.ClienteId;
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await utenti.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("Esiste già un utente con questa email.");
        }

        var utente = new Utente
        {
            Email = email,
            Nome = request.Nome,
            Cognome = request.Cognome,
            IsSuperAdmin = request.IsSuperAdmin,
            ClienteId = clienteId,
        };
        utente.PasswordHash = passwordHasher.HashPassword(utente, request.Password);

        await utenti.AddAsync(utente, cancellationToken);
        return utente;
    }

    public async Task<UtenteStruttura> AssegnaRuoloAsync(
        ICurrentUser currentUser,
        Guid utenteId,
        Guid strutturaId,
        AssegnaRuoloRequest request,
        CancellationToken cancellationToken)
    {
        var utenteTarget = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");
        var strutturaClienteId = await strutture.GetClienteIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        if (!currentUser.IsSuperAdmin)
        {
            if (currentUser.ClienteId != strutturaClienteId || utenteTarget.ClienteId != currentUser.ClienteId)
            {
                throw new ForbiddenException("Non puoi assegnare ruoli su questa struttura/utente.");
            }
        }

        var assegnazione = await utentiStrutture.GetAsync(utenteId, strutturaId, cancellationToken)
            ?? new UtenteStruttura { UtenteId = utenteId, StrutturaId = strutturaId };

        assegnazione.Ruolo = request.Ruolo;
        assegnazione.BookingRead = request.BookingRead;
        assegnazione.BookingWrite = request.BookingWrite;
        assegnazione.ReservationRead = request.ReservationRead;
        assegnazione.ReservationWrite = request.ReservationWrite;
        assegnazione.StatePoliceRead = request.StatePoliceRead;
        assegnazione.StatePoliceWrite = request.StatePoliceWrite;
        assegnazione.StatePoliceSettings = request.StatePoliceSettings;
        assegnazione.SettingAgency = request.SettingAgency;
        assegnazione.SettingUser = request.SettingUser;
        assegnazione.SettingRoomRead = request.SettingRoomRead;
        assegnazione.SettingRoomWrite = request.SettingRoomWrite;
        assegnazione.RoomStatusUpdate = request.RoomStatusUpdate;
        assegnazione.FinanceRead = request.FinanceRead;
        assegnazione.FinanceWrite = request.FinanceWrite;
        assegnazione.RestaurantRead = request.RestaurantRead;
        assegnazione.RestaurantWrite = request.RestaurantWrite;

        await utentiStrutture.UpsertAsync(assegnazione, cancellationToken);
        return assegnazione;
    }

    public async Task CambiaPasswordAsync(ICurrentUser currentUser, CambiaPasswordRequest request, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByIdAsync(currentUser.UtenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        var esito = passwordHasher.VerifyHashedPassword(utente, utente.PasswordHash, request.PasswordAttuale);
        if (esito == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAppException("La password attuale non è corretta.");
        }

        utente.PasswordHash = passwordHasher.HashPassword(utente, request.PasswordNuova);
        await utenti.UpdateAsync(utente, cancellationToken);
    }
}
