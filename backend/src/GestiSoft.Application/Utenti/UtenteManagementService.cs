using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace GestiSoft.Application.Utenti;

public record CreaUtenteRequest(string Email, string Password, string? Nome, string? Cognome, bool IsSuperAdmin, Guid? ClienteId, bool IsClienteAccount = false);

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

/// <summary>Tutti i permessi granulari dell'utente corrente su una Struttura — usato dal frontend per decidere quali voci di menu mostrare.</summary>
public record MioPermessoRisultato(
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
    bool RestaurantWrite)
{
    public static readonly MioPermessoRisultato Tutti = new(true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true);
    public static readonly MioPermessoRisultato Nessuno = new(false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false);

    public static MioPermessoRisultato Da(UtenteStruttura a) => new(
        a.BookingRead, a.BookingWrite, a.ReservationRead, a.ReservationWrite,
        a.StatePoliceRead, a.StatePoliceWrite, a.StatePoliceSettings,
        a.SettingAgency, a.SettingUser, a.SettingRoomRead, a.SettingRoomWrite, a.RoomStatusUpdate,
        a.FinanceRead, a.FinanceWrite, a.RestaurantRead, a.RestaurantWrite);
}

public class UtenteManagementService(
    IUtenteRepository utenti,
    IUtenteStrutturaRepository utentiStrutture,
    IStrutturaRepository strutture,
    TenantAccessGuard accessGuard,
    IPasswordHasher<Utente> passwordHasher,
    ILogEventoService logEventi)
{
    public async Task<IReadOnlyList<Utente>> ListaUtentiClienteAsync(ICurrentUser currentUser, Guid clienteId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin && currentUser.ClienteId != clienteId)
        {
            throw new ForbiddenException("Non puoi consultare gli utenti di questo Cliente.");
        }

        if (!await HaGestioneUtentiClienteAsync(currentUser, clienteId, cancellationToken))
        {
            throw new ForbiddenException("Solo chi gestisce gli utenti può consultare questo elenco.");
        }

        return await utenti.ListByClienteIdAsync(clienteId, cancellationToken);
    }

    /// <summary>
    /// "Gestione utenti" a livello di Cliente (non di singola Struttura): il Super Admin e il
    /// titolare (Utente.IsClienteAccount) hanno sempre accesso, per un utente normale basta il
    /// permesso SettingUser su almeno una Struttura di quel Cliente.
    /// </summary>
    private async Task<bool> HaGestioneUtentiClienteAsync(ICurrentUser currentUser, Guid clienteId, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return true;
        }

        var utenteCorrente = await utenti.GetByIdAsync(currentUser.UtenteId, cancellationToken);
        if (utenteCorrente is { IsClienteAccount: true })
        {
            return true;
        }

        return await utentiStrutture.HaGestioneUtentiClienteAsync(currentUser.UtenteId, clienteId, cancellationToken);
    }

    public async Task<IReadOnlyList<UtenteStruttura>> ListaAssegnazioniStrutturaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        // Stesso controllo (Cliente/Attivo/licenza) di ogni altro modulo operativo — prima d'ora
        // questo endpoint bypassava TenantAccessGuard, restando utilizzabile anche su una Struttura a
        // licenza scaduta, incoerente col resto dell'app (Camere/Prenotazioni/... la bloccano già).
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);

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
    /// la pagina log deve vedere"). Il Super Admin ha sempre accesso, non ha una riga UtenteStruttura;
    /// il titolare del Cliente (Utente.IsClienteAccount) allo stesso modo, per lo stesso motivo.
    /// </summary>
    public async Task<bool> HaGestioneUtentiAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return true;
        }

        var utenteCorrente = await utenti.GetByIdAsync(currentUser.UtenteId, cancellationToken);
        if (utenteCorrente is { IsClienteAccount: true })
        {
            return true;
        }

        var assegnazione = await utentiStrutture.GetAsync(currentUser.UtenteId, strutturaId, cancellationToken);
        return assegnazione?.SettingUser ?? false;
    }

    /// <summary>
    /// Tutti i permessi granulari dell'utente corrente su questa Struttura — usato dal frontend per
    /// decidere quali voci di menu mostrare (su richiesta esplicita: un addetto pulizie non deve
    /// vedere Calendario/Finanze/Invii automatici/ecc., solo le pagine per cui ha davvero un
    /// permesso). Stesso bypass di <see cref="HaGestioneUtentiAsync"/>: Super Admin e titolare del
    /// Cliente vedono/possono tutto.
    /// </summary>
    public async Task<MioPermessoRisultato> GetMioPermessoAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return MioPermessoRisultato.Tutti;
        }

        var utenteCorrente = await utenti.GetByIdAsync(currentUser.UtenteId, cancellationToken);
        if (utenteCorrente is { IsClienteAccount: true })
        {
            return MioPermessoRisultato.Tutti;
        }

        var assegnazione = await utentiStrutture.GetAsync(currentUser.UtenteId, strutturaId, cancellationToken);
        return assegnazione is null ? MioPermessoRisultato.Nessuno : MioPermessoRisultato.Da(assegnazione);
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

            if (!await HaGestioneUtentiClienteAsync(currentUser, clienteId!.Value, cancellationToken))
            {
                throw new ForbiddenException("Solo chi gestisce gli utenti può creare nuovi utenti.");
            }
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await utenti.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("Esiste già un utente con questa email.");
        }

        // IsClienteAccount (titolare, accesso libero a tutte le Strutture del Cliente) è una leva
        // sensibile quanto IsSuperAdmin: onorata solo se richiesta da un Super Admin, altrimenti
        // ignorata silenziosamente (mai un privilege escalation che un Cliente può concedersi da solo).
        var utente = new Utente
        {
            Email = email,
            Nome = request.Nome,
            Cognome = request.Cognome,
            IsSuperAdmin = request.IsSuperAdmin,
            ClienteId = clienteId,
            IsClienteAccount = currentUser.IsSuperAdmin && request.IsClienteAccount,
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

        if (currentUser.IsSuperAdmin)
        {
            _ = await strutture.GetClienteIdAsync(strutturaId, cancellationToken)
                ?? throw new NotFoundException("Struttura non trovata.");
        }
        else
        {
            // Stesso controllo (Cliente/Attivo/licenza) di ogni altro modulo operativo — prima d'ora
            // si poteva assegnare/modificare un ruolo anche su una Struttura a licenza scaduta.
            await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);

            if (utenteTarget.ClienteId != currentUser.ClienteId)
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

    /// <summary>
    /// Rimuove l'accesso di un Utente a questa Struttura — non elimina l'account Utente in sé, che
    /// resta (con le eventuali altre assegnazioni su altre Strutture dello stesso Cliente): "eliminare
    /// l'utente" dalla pagina Utenti di una Struttura significa qui togliergli l'accesso a QUELLA
    /// struttura, non cancellare la sua identità dal sistema.
    /// </summary>
    public async Task RimuoviAssegnazioneAsync(ICurrentUser currentUser, Guid utenteId, Guid strutturaId, CancellationToken cancellationToken)
    {
        var utenteTarget = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (currentUser.IsSuperAdmin)
        {
            _ = await strutture.GetClienteIdAsync(strutturaId, cancellationToken)
                ?? throw new NotFoundException("Struttura non trovata.");
        }
        else
        {
            // Stesso controllo (Cliente/Attivo/licenza) di ogni altro modulo operativo — prima d'ora
            // si poteva rimuovere un accesso anche su una Struttura a licenza scaduta.
            await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);

            if (utenteTarget.ClienteId != currentUser.ClienteId)
            {
                throw new ForbiddenException("Non puoi rimuovere l'accesso su questa struttura/utente.");
            }

            if (!await HaGestioneUtentiAsync(currentUser, strutturaId, cancellationToken))
            {
                throw new ForbiddenException("Solo chi gestisce gli utenti di questa struttura può rimuovere un accesso.");
            }
        }

        if (utenteId == currentUser.UtenteId)
        {
            throw new ConflictException("Non puoi rimuovere il tuo stesso accesso a questa struttura.");
        }

        await utentiStrutture.RemoveAsync(utenteId, strutturaId, cancellationToken);
        await LogUtenteAsync(currentUser, utenteTarget.ClienteId, strutturaId, $"Accesso rimosso per {utenteTarget.Email}.", cancellationToken);
    }

    public async Task<Utente> AggiornaAsync(ICurrentUser currentUser, Guid utenteId, AggiornaUtenteRequest request, CancellationToken cancellationToken)
    {
        var utenteTarget = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (!currentUser.IsSuperAdmin && utenteTarget.ClienteId != currentUser.ClienteId)
        {
            throw new ForbiddenException("Non puoi modificare questo utente.");
        }

        if (!await HaGestioneUtentiClienteAsync(currentUser, utenteTarget.ClienteId!.Value, cancellationToken))
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

        if (!await HaGestioneUtentiClienteAsync(currentUser, utenteTarget.ClienteId!.Value, cancellationToken))
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
