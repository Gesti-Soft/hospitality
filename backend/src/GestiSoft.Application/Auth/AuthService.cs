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
    private const string CredenzialiNonValideMessage = "Email o password non corretti.";

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var emailNormalizzata = email.Trim().ToLowerInvariant();
        var utente = await utenti.GetByEmailAsync(emailNormalizzata, cancellationToken);

        // Stesso messaggio sia per utente inesistente che per password errata: non rivelare
        // quale dei due sia il problema (evita di confermare a un attaccante che un'email esiste).
        // Stesso trattamento per un Cliente sospeso dal Super Admin (mancato pagamento, ecc.):
        // nessun utente di quel Cliente deve poter accedere, anche con credenziali corrette.
        // Categoria "Auth" non è mai visibile al Cliente (solo il Super Admin vede login/logout,
        // vedi LogVisibilita) — libero di loggare anche i tentativi falliti senza rischio di fuga.
        if (utente is null || !utente.Attivo)
        {
            await LogFallitoAsync(emailNormalizzata, utente?.ClienteId, cancellationToken);
            throw new UnauthorizedAppException(CredenzialiNonValideMessage);
        }

        if (utente.ClienteId is { } clienteId)
        {
            var cliente = await clienti.GetByIdAsync(clienteId, cancellationToken);
            if (cliente is null || !cliente.Attivo)
            {
                await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
                throw new UnauthorizedAppException(CredenzialiNonValideMessage);
            }
        }

        var esito = passwordHasher.VerifyHashedPassword(utente, utente.PasswordHash, password);
        if (esito == PasswordVerificationResult.Failed)
        {
            await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
            throw new UnauthorizedAppException(CredenzialiNonValideMessage);
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

    private Task LogFallitoAsync(string emailTentata, Guid? clienteId, CancellationToken cancellationToken) =>
        logEventi.RegistraAsync(
            LivelloLog.Warning,
            $"Login fallito per '{emailTentata}'.",
            origine: "Auth",
            clienteId: clienteId,
            categoria: "Auth",
            cancellationToken: cancellationToken);
}
