using GestiSoft.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    public record LoginRequest(string Email, string Password);

    public record LoginResponse(string Token, DateTime ScadeAtUtc, Guid UtenteId, string Email, bool IsSuperAdmin, Guid? ClienteId);

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var esito = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
        return Ok(new LoginResponse(esito.Token, esito.ScadeAtUtc, esito.UtenteId, esito.Email, esito.IsSuperAdmin, esito.ClienteId));
    }
}
