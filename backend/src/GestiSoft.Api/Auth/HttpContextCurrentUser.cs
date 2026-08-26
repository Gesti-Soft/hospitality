using System.IdentityModel.Tokens.Jwt;
using GestiSoft.Application.Auth;

namespace GestiSoft.Api.Auth;

public class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid UtenteId => Guid.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;

    public string Email => Principal?.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;

    public bool IsSuperAdmin => Principal?.FindFirst(AppClaimTypes.IsSuperAdmin)?.Value == "true";

    public Guid? ClienteId => Guid.TryParse(Principal?.FindFirst(AppClaimTypes.ClienteId)?.Value, out var id) ? id : null;
}
