using GestiSoft.Application.Clienti;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace GestiSoft.Application.Auth;

public record LoginResult(string Token, DateTime ScadeAtUtc, Guid UtenteId, string Email, bool IsSuperAdmin, Guid? ClienteId);

public class AuthService(
    IUtenteRepository utenti,
    IClienteRepository clienti,
    IPasswordHasher<Utente> passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ILogEventoService logEventi)
{
    // Messaggio generico riservato al rinnovo silenzioso (RefreshAsync) — lì non è mai mostrato in
    // un form, l'utente scopre la sessione scaduta solo al prossimo 401 su un'azione reale.
    private const string CredenzialiNonValideMessage = "Sessione non valida.";

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var emailNormalizzata = email.Trim().ToLowerInvariant();
        var utente = await utenti.GetByEmailAsync(emailNormalizzata, cancellationToken);

        // Messaggi specifici per caso, su richiesta esplicita dell'utente (rinuncia deliberata alla
        // protezione anti-enumerazione che c'era prima — un messaggio unico per ogni causa di
        // fallimento — perché qui conta di più poter distinguere a colpo d'occhio "email sbagliata"
        // da "password sbagliata" da "utente disabilitato"). Categoria "Auth" non è mai visibile al
        // Cliente (solo il Super Admin vede login/logout, vedi LogVisibilita).
        if (utente is null)
        {
            await LogFallitoAsync(emailNormalizzata, null, cancellationToken);
            throw new UnauthorizedAppException("Email non trovata.");
        }

        if (!utente.Attivo)
        {
            await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
            throw new UnauthorizedAppException("Utente disabilitato. Contatta l'amministrazione.");
        }

        if (utente.ClienteId is { } clienteId)
        {
            var cliente = await clienti.GetByIdAsync(clienteId, cancellationToken);
            if (cliente is null || !cliente.Attivo)
            {
                await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
                throw new UnauthorizedAppException("Il tuo account è stato sospeso. Contatta l'amministrazione.");
            }
        }

        var esito = passwordHasher.VerifyHashedPassword(utente, utente.PasswordHash, password);
        if (esito == PasswordVerificationResult.Failed)
        {
            await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
            throw new UnauthorizedAppException("Password errata.");
        }

        var token = tokenGenerator.Generate(utente);

        await logEventi.RegistraAsync(
            LivelloLog.Info,
            "Login effettuato.",
            origine: "Auth",
            clienteId: utente.ClienteId,
            categoria: "Auth",
            operatore: utente.Email,
            cancellationToken: cancellationToken);

        return new LoginResult(token.Value, token.ScadeAtUtc, utente.Id, utente.Email, utente.IsSuperAdmin, utente.ClienteId);
    }

    /// <summary>
    /// Rinnova il token di un utente già autenticato (chiamato dal frontend in background mentre
    /// l'utente è attivo) — stessi controlli del login (utente/Cliente attivi) ma senza password,
    /// il chiamante deve già possedere un JWT valido e non scaduto (endpoint protetto da
    /// [Authorize]). Nessun LogEvento qui: a differenza del login vero e proprio, un rinnovo può
    /// avvenire più volte l'ora e non è un evento significativo da mostrare in pagina Log.
    /// </summary>
    public async Task<LoginResult> RefreshAsync(Guid utenteId, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken);
        if (utente is null || !utente.Attivo)
        {
            throw new UnauthorizedAppException(CredenzialiNonValideMessage);
        }

        if (utente.ClienteId is { } clienteId)
        {
            var cliente = await clienti.GetByIdAsync(clienteId, cancellationToken);
            if (cliente is null || !cliente.Attivo)
            {
                throw new UnauthorizedAppException(CredenzialiNonValideMessage);
            }
        }

        var token = tokenGenerator.Generate(utente);
        return new LoginResult(token.Value, token.ScadeAtUtc, utente.Id, utente.Email, utente.IsSuperAdmin, utente.ClienteId);
    }

    private Task LogFallitoAsync(string emailTentata, Guid? clienteId, CancellationToken cancellationToken) =>
        logEventi.RegistraAsync(
            LivelloLog.Warning,
            $"Login fallito per '{emailTentata}'.",
            origine: "Auth",
            clienteId: clienteId,
            categoria: "Auth",
            cancellationToken: cancellationToken);
}
