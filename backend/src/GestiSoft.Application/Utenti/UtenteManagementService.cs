using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
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

public record AggiornaUtenteRequest(string? Nome, string? Cognome, string Email);

public record ResetPasswordRequest(string PasswordNuova);

public class UtenteManagementService(
    IUtenteRepository utenti,
    IUtenteStrutturaRepository utentiStrutture,
    IStrutturaRepository strutture,
    IPasswordHasher<Utente> passwordHasher,
    ILogEventoService logEventi)
{
    public async Task<IReadOnlyList<Utente>> ListaUtentiClienteAsync(ICurrentUser currentUser, Guid clienteId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin && currentUser.ClienteId != clienteId)
        {
            throw new ForbiddenException("Non puoi consultare gli utenti di questo Cliente.");
        }

        if (!currentUser.IsSuperAdmin && !await utentiStrutture.HaGestioneUtentiClienteAsync(currentUser.UtenteId, clienteId, cancellationToken))
        {
            throw new ForbiddenException("Solo chi gestisce gli utenti può consultare questo elenco.");
        }

        return await utenti.ListByClienteIdAsync(clienteId, cancellationToken);
    }

    public async Task<IReadOnlyList<UtenteStruttura>> ListaAssegnazioniStrutturaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        var strutturaClienteId = await strutture.GetClienteIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        if (!currentUser.IsSuperAdmin && currentUser.ClienteId != strutturaClienteId)
        {
            throw new ForbiddenException("Non puoi consultare gli utenti di questa struttura.");
        }

        if (!await HaGestioneUtentiAsync(currentUser, strutturaId, cancellationToken))
        {
            throw new ForbiddenException("Solo chi gestisce gli utenti di questa struttura può consultare questo elenco.");
        }

        return await utentiStrutture.ListByStrutturaIdAsync(strutturaId, cancellationToken);
    }

    /// <summary>
    /// "Gestione utenti" (permesso SettingUser) è qui usato anche come porta d'accesso alla pagina
    /// Log: solo chi può gestire gli utenti di una struttura deve poter vedere cosa succede su quella
    /// struttura, i lavoratori normali no (richiesta esplicita — "l'utente non amministratore nemmeno
    /// la pagina log deve vedere"). Il Super Admin ha sempre accesso, non ha una riga UtenteStruttura.
    /// </summary>
    public async Task<bool> HaGestioneUtentiAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return true;
        }

        var assegnazione = await utentiStrutture.GetAsync(currentUser.UtenteId, strutturaId, cancellationToken);
        return assegnazione?.SettingUser ?? false;
    }

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

            if (!await utentiStrutture.HaGestioneUtentiClienteAsync(currentUser.UtenteId, clienteId!.Value, cancellationToken))
            {
                throw new ForbiddenException("Solo chi gestisce gli utenti può creare nuovi utenti.");
            }
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
        await LogUtenteAsync(currentUser, clienteId, null, $"Utente creato ({utente.Email}).", cancellationToken);
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

            // Bug reale trovato mentre si irrobustiva l'accesso alla sezione Amministrazione: prima
            // d'ora QUALSIASI utente dello stesso Cliente poteva assegnare ruoli/permessi (anche
            // SettingUser a se stesso) senza già avere il permesso di gestione utenti — un lavoratore
            // poteva auto-promuoversi Administrator. Richiede ora lo stesso permesso SettingUser
            // controllato per la pagina Log/Utenti.
            if (!await HaGestioneUtentiAsync(currentUser, strutturaId, cancellationToken))
            {
                throw new ForbiddenException("Solo chi gestisce gli utenti di questa struttura può assegnare ruoli.");
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
        await LogUtenteAsync(currentUser, utenteTarget.ClienteId, strutturaId, $"Ruolo/permessi aggiornati per {utenteTarget.Email}.", cancellationToken);
        return assegnazione;
    }

    public async Task<Utente> AggiornaAsync(ICurrentUser currentUser, Guid utenteId, AggiornaUtenteRequest request, CancellationToken cancellationToken)
    {
        var utenteTarget = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (!currentUser.IsSuperAdmin && utenteTarget.ClienteId != currentUser.ClienteId)
        {
            throw new ForbiddenException("Non puoi modificare questo utente.");
        }

        if (!currentUser.IsSuperAdmin && !await utentiStrutture.HaGestioneUtentiClienteAsync(currentUser.UtenteId, utenteTarget.ClienteId!.Value, cancellationToken))
        {
            throw new ForbiddenException("Solo chi gestisce gli utenti può modificare questo utente.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var esistente = await utenti.GetByEmailAsync(email, cancellationToken);
        if (esistente is not null && esistente.Id != utenteId)
        {
            throw new ConflictException("Esiste già un utente con questa email.");
        }

        utenteTarget.Email = email;
        utenteTarget.Nome = string.IsNullOrWhiteSpace(request.Nome) ? null : request.Nome.Trim();
        utenteTarget.Cognome = string.IsNullOrWhiteSpace(request.Cognome) ? null : request.Cognome.Trim();

        await utenti.UpdateAsync(utenteTarget, cancellationToken);
        await LogUtenteAsync(currentUser, utenteTarget.ClienteId, null, $"Profilo utente aggiornato ({utenteTarget.Email}).", cancellationToken);
        return utenteTarget;
    }

    /// <summary>Reset "di supporto": chi gestisce gli utenti del proprio Cliente imposta direttamente
    /// una nuova password su un altro utente, senza dover conoscere quella attuale (a differenza di
    /// CambiaPasswordAsync, pensato per l'utente che cambia la propria).</summary>
    public async Task ResetPasswordAsync(ICurrentUser currentUser, Guid utenteId, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var utenteTarget = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (!currentUser.IsSuperAdmin && utenteTarget.ClienteId != currentUser.ClienteId)
        {
            throw new ForbiddenException("Non puoi modificare questo utente.");
        }

        if (!currentUser.IsSuperAdmin && !await utentiStrutture.HaGestioneUtentiClienteAsync(currentUser.UtenteId, utenteTarget.ClienteId!.Value, cancellationToken))
        {
            throw new ForbiddenException("Solo chi gestisce gli utenti può modificare questo utente.");
        }

        utenteTarget.PasswordHash = passwordHasher.HashPassword(utenteTarget, request.PasswordNuova);
        await utenti.UpdateAsync(utenteTarget, cancellationToken);
        await LogUtenteAsync(currentUser, utenteTarget.ClienteId, null, $"Password reimpostata per {utenteTarget.Email}.", cancellationToken);
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
        await LogUtenteAsync(currentUser, utente.ClienteId, null, "Password modificata dall'utente stesso.", cancellationToken);
    }

    /// <summary>
    /// Categoria "Utente" — visibile anche al Cliente (vedi LogVisibilita) — solo quando è stato
    /// davvero il Cliente ad agire sui propri utenti. Se invece è il Super Admin ad agire su un
    /// utente per conto di un Cliente (stessi metodi, richiamabili da entrambi: qui non c'è il
    /// vincolo RichiediSuperAdmin di SuperAdminService), va in "SuperAdmin" — mai visibile al
    /// Cliente, coerente con "tutto quello che fa l'admin il cliente non deve vederlo".
    /// </summary>
    private Task LogUtenteAsync(ICurrentUser currentUser, Guid? clienteId, Guid? strutturaId, string messaggio, CancellationToken cancellationToken) =>
        logEventi.RegistraAsync(
            LivelloLog.Info,
            messaggio,
            origine: "Utenti",
            clienteId: clienteId,
            strutturaId: strutturaId,
            categoria: currentUser.IsSuperAdmin ? "SuperAdmin" : "Utente",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);
}
