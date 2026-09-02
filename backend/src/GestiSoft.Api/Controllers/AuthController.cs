using GestiSoft.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService authService, ICurrentUser currentUser) : ControllerBase
{
    public record LoginRequest(string Email, string Password);

    public record LoginResponse(string Token, DateTime ScadeAtUtc, Guid UtenteId, string Email, bool IsSuperAdmin, Guid? ClienteId);

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var esito = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
        return Ok(new LoginResponse(esito.Token, esito.ScadeAtUtc, esito.UtenteId, esito.Email, esito.IsSuperAdmin, esito.ClienteId));
    }

    /// <summary>Chiamato dal frontend in background mentre l'utente è attivo, per tenere viva la sessione senza richiedere un nuovo login.</summary>
    [Authorize]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken cancellationToken)
    {
        var esito = await authService.RefreshAsync(currentUser.UtenteId, cancellationToken);
        return Ok(new LoginResponse(esito.Token, esito.ScadeAtUtc, esito.UtenteId, esito.Email, esito.IsSuperAdmin, esito.ClienteId));
    }
}
