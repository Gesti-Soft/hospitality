using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Auth;

public interface IJwtTokenGenerator
{
    JwtToken Generate(Utente utente);
}

public record JwtToken(string Value, DateTime ScadeAtUtc);
