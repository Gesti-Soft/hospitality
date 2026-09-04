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
}
