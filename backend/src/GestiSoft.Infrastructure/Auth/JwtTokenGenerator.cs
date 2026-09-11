using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestiSoft.Application.Auth;
using GestiSoft.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GestiSoft.Infrastructure.Auth;

public class JwtTokenGenerator(IOptions<JwtOptions> options) : IJwtTokenGenerator
{
    /// <summary>Quanto tempo ha l'utente per aprire l'app e digitare il codice: abbastanza per non rifare il login, troppo poco perché un token rubato serva a qualcosa.</summary>
    private const int MinutiValiditaTokenVerifica = 5;

    public JwtToken Generate(Utente utente)
    {
        var opts = options.Value;
        var scadeAtUtc = DateTime.UtcNow.AddMinutes(opts.LifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, utente.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, utente.Email),
            new(AppClaimTypes.IsSuperAdmin, utente.IsSuperAdmin ? "true" : "false"),
            new(AppClaimTypes.IsClienteAccount, utente.IsClienteAccount ? "true" : "false"),
        };

        if (utente.ClienteId is { } clienteId)
        {
            claims.Add(new Claim(AppClaimTypes.ClienteId, clienteId.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: claims,
            expires: scadeAtUtc,
            signingCredentials: credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);
        return new JwtToken(value, scadeAtUtc);
    }

    /// <summary>
    /// Stessa firma del token normale ma audience dedicata e vita corta: se qualcuno provasse a
    /// usarlo come token di sessione, la validazione dell'audience configurata in Program.cs lo
    /// scarterebbe — non serve un controllo in più su ogni endpoint.
    /// </summary>
    public JwtToken GeneraTokenVerifica2Fa(Utente utente)
    {
        var opts = options.Value;
        var scadeAtUtc = DateTime.UtcNow.AddMinutes(MinutiValiditaTokenVerifica);

        var token = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: AudienceVerifica2Fa(opts),
            claims: [new Claim(JwtRegisteredClaimNames.Sub, utente.Id.ToString())],
            expires: scadeAtUtc,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Secret)),
                SecurityAlgorithms.HmacSha256));

        return new JwtToken(new JwtSecurityTokenHandler().WriteToken(token), scadeAtUtc);
    }

    public Guid? LeggiUtenteDaTokenVerifica2Fa(string token)
    {
        var opts = options.Value;
        var parametri = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opts.Issuer,
            ValidateAudience = true,
            ValidAudience = AudienceVerifica2Fa(opts),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, parametri, out _);
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var utenteId) ? utenteId : null;
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    private static string AudienceVerifica2Fa(JwtOptions opts) => $"{opts.Audience}.2fa";
}
