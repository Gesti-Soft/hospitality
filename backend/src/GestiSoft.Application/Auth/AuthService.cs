using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace GestiSoft.Application.Auth;

public record LoginResult(string Token, DateTime ScadeAtUtc, Guid UtenteId, string Email, bool IsSuperAdmin, Guid? ClienteId);

public class AuthService(
    IUtenteRepository utenti,
    IPasswordHasher<Utente> passwordHasher,
    IJwtTokenGenerator tokenGenerator)
{
    private const string CredenzialiNonValideMessage = "Email o password non corretti.";

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);

        // Stesso messaggio sia per utente inesistente che per password errata: non rivelare
        // quale dei due sia il problema (evita di confermare a un attaccante che un'email esiste).
        if (utente is null || !utente.Attivo)
        {
            throw new UnauthorizedAppException(CredenzialiNonValideMessage);
        }

        var esito = passwordHasher.VerifyHashedPassword(utente, utente.PasswordHash, password);
        if (esito == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAppException(CredenzialiNonValideMessage);
        }

        var token = tokenGenerator.Generate(utente);

        return new LoginResult(token.Value, token.ScadeAtUtc, utente.Id, utente.Email, utente.IsSuperAdmin, utente.ClienteId);
    }
}
