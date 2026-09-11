using GestiSoft.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService authService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// <paramref name="TokenDispositivo"/>: quello ricevuto da una verifica precedente su questo
    /// browser ("ricorda questo dispositivo"). Se è ancora valido il codice non viene richiesto.
    /// </summary>
    public record LoginRequest(string Email, string Password, string? TokenDispositivo);

    public record LoginResponse(string Token, DateTime ScadeAtUtc, Guid UtenteId, string Email, bool IsSuperAdmin, Guid? ClienteId, bool IsClienteAccount);

    /// <summary>
    /// Esito del primo passo: o la sessione (<paramref name="Sessione"/>), o la richiesta del
    /// codice con il token da rimandare indietro. Mai entrambi.
    /// </summary>
    public record EsitoLoginResponse(bool Richiede2Fa, LoginResponse? Sessione, string? TokenVerifica2Fa);

    public record Verifica2FaRequest(string TokenVerifica2Fa, string Codice, bool RicordaDispositivo);

    public record Verifica2FaResponse(LoginResponse Sessione, string? TokenDispositivo, DateTime? DispositivoScadeAtUtc, int CodiciRecuperoRimasti);

    public record Avvia2FaResponse(string Secret, string UriOtpauth);

    public record Attiva2FaRequest(string Codice);

    public record CodiciRecuperoResponse(IReadOnlyList<string> Codici);

    public record ConfermaPasswordRequest(string Password);

    public record Stato2FaResponse(bool Attivo, int CodiciRimasti);

    /// <summary>
    /// Limite dedicato (vedi Program.cs): il tetto generale dell'Api è pensato per l'uso normale
    /// dell'applicazione e su questo endpoint lascerebbe passare decine di tentativi al secondo.
    /// </summary>
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [HttpPost("login")]
    public async Task<ActionResult<EsitoLoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var esito = await authService.LoginAsync(request.Email, request.Password, request.TokenDispositivo, cancellationToken);
        return Ok(new EsitoLoginResponse(esito.TokenVerifica2Fa is not null, ToResponse(esito.Sessione), esito.TokenVerifica2Fa));
    }

    [EnableRateLimiting(RateLimitPolicies.Login)]
    [HttpPost("2fa/verifica")]
    public async Task<ActionResult<Verifica2FaResponse>> Verifica2Fa([FromBody] Verifica2FaRequest request, CancellationToken cancellationToken)
    {
        var esito = await authService.Verifica2FaAsync(request.TokenVerifica2Fa, request.Codice, request.RicordaDispositivo, cancellationToken);
        return Ok(new Verifica2FaResponse(
            ToResponse(esito.Sessione)!, esito.TokenDispositivo, esito.DispositivoScadeAtUtc, esito.CodiciRecuperoRimasti));
    }

    /// <summary>Chiamato dal frontend in background mentre l'utente è attivo, per tenere viva la sessione senza richiedere un nuovo login.</summary>
    [Authorize]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken cancellationToken)
    {
        var esito = await authService.RefreshAsync(currentUser.UtenteId, cancellationToken);
        return Ok(ToResponse(esito));
    }

    [Authorize]
    [HttpGet("2fa")]
    public async Task<ActionResult<Stato2FaResponse>> Stato2Fa(CancellationToken cancellationToken)
    {
        var (attivo, rimasti) = await authService.Stato2FaAsync(currentUser.UtenteId, cancellationToken);
        return Ok(new Stato2FaResponse(attivo, rimasti));
    }

    [Authorize]
    [HttpPost("2fa/avvia")]
    public async Task<ActionResult<Avvia2FaResponse>> Avvia2Fa(CancellationToken cancellationToken)
    {
        var avvio = await authService.Avvia2FaAsync(currentUser.UtenteId, cancellationToken);
        return Ok(new Avvia2FaResponse(avvio.Secret, avvio.UriOtpauth));
    }

    [Authorize]
    [HttpPost("2fa/attiva")]
    public async Task<ActionResult<CodiciRecuperoResponse>> Attiva2Fa([FromBody] Attiva2FaRequest request, CancellationToken cancellationToken)
    {
        var codici = await authService.Attiva2FaAsync(currentUser.UtenteId, request.Codice, cancellationToken);
        return Ok(new CodiciRecuperoResponse(codici));
    }

    [Authorize]
    [HttpPost("2fa/disattiva")]
    public async Task<IActionResult> Disattiva2Fa([FromBody] ConfermaPasswordRequest request, CancellationToken cancellationToken)
    {
        await authService.Disattiva2FaAsync(currentUser.UtenteId, request.Password, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("2fa/codici-recupero")]
    public async Task<ActionResult<CodiciRecuperoResponse>> RigeneraCodici([FromBody] ConfermaPasswordRequest request, CancellationToken cancellationToken)
    {
        var codici = await authService.RigeneraCodiciAsync(currentUser.UtenteId, request.Password, cancellationToken);
        return Ok(new CodiciRecuperoResponse(codici));
    }

    private static LoginResponse? ToResponse(LoginResult? esito) => esito is null
        ? null
        : new LoginResponse(esito.Token, esito.ScadeAtUtc, esito.UtenteId, esito.Email, esito.IsSuperAdmin, esito.ClienteId, esito.IsClienteAccount);
}
