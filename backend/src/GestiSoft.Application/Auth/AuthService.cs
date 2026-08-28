using GestiSoft.Application.Clienti;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace GestiSoft.Application.Auth;

public record LoginResult(string Token, DateTime ScadeAtUtc, Guid UtenteId, string Email, bool IsSuperAdmin, Guid? ClienteId);

public class AuthService(
    IUtenteRepository utenti,
    IClienteRepository clienti,
    IPasswordHasher<Utente> passwordHasher,
    IJwtTokenGenerator tokenGenerator)
{
    private const string CredenzialiNonValideMessage = "Email o password non corretti.";

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);

        // Stesso messaggio sia per utente inesistente che per password errata: non rivelare
        // quale dei due sia il problema (evita di confermare a un attaccante che un'email esiste).
        // Stesso trattamento per un Cliente sospeso dal Super Admin (mancato pagamento, ecc.):
        // nessun utente di quel Cliente deve poter accedere, anche con credenziali corrette.
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

        var esito = passwordHasher.VerifyHashedPassword(utente, utente.PasswordHash, password);
        if (esito == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAppException(CredenzialiNonValideMessage);
        }

        var token = tokenGenerator.Generate(utente);

        return new LoginResult(token.Value, token.ScadeAtUtc, utente.Id, utente.Email, utente.IsSuperAdmin, utente.ClienteId);
    }
}
