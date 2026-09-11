using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Auth;

public interface IJwtTokenGenerator
{
    JwtToken Generate(Utente utente);

    /// <summary>
    /// Token intermedio del login in due passi: dice solo "questo utente ha già superato la
    /// password", dura pochi minuti e non apre nulla — è emesso con un'audience diversa, quindi la
    /// validazione standard degli endpoint protetti lo rifiuta. Vale solo per
    /// <see cref="LeggiUtenteDaTokenVerifica2Fa"/>.
    /// </summary>
    JwtToken GeneraTokenVerifica2Fa(Utente utente);

    /// <summary>Id dell'utente dentro un token di verifica valido e non scaduto, null in ogni altro caso.</summary>
    Guid? LeggiUtenteDaTokenVerifica2Fa(string token);
}

public record JwtToken(string Value, DateTime ScadeAtUtc);
