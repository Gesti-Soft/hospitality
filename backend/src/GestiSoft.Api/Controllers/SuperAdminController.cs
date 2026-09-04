using GestiSoft.Application.Auth;
using ApplicationSuperAdmin = GestiSoft.Application.SuperAdmin;
using GestiSoft.Contracts.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>
/// Dashboard e leve di gestione cross-Cliente per il Super Admin (staff GestiSoft): panoramica
/// Clienti/Strutture/Utenti/integrazioni, sospensione Cliente, concessione dei servizi esterni per
/// Cliente, reset password di supporto — nessuna rotta annidata sotto strutture/{id}, dato che il
/// punto è vedere/gestire TUTTI i Clienti insieme.
/// </summary>
[ApiController]
[Route("super-admin")]
[Authorize]
public class SuperAdminController(ApplicationSuperAdmin.SuperAdminService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var dashboard = await service.GetDashboardAsync(currentUser, cancellationToken);
        return Ok(ToDto(dashboard));
    }

    [HttpPut("clienti/{clienteId:guid}/attivo")]
    public async Task<IActionResult> ImpostaAttivo(Guid clienteId, [FromBody] ImpostaAttivoRequest request, CancellationToken cancellationToken)
    {
        var cliente = await service.ImpostaAttivoAsync(currentUser, clienteId, request.Attivo, cancellationToken);
        return Ok(new { cliente.Id, cliente.Attivo });
    }

    [HttpPut("strutture/{strutturaId:guid}/servizi")]
    public async Task<IActionResult> AggiornaServizi(Guid strutturaId, [FromBody] ServiziStrutturaRequest request, CancellationToken cancellationToken)
    {
        var struttura = await service.AggiornaServiziAsync(
            currentUser,
            strutturaId,
            new ApplicationSuperAdmin.ServiziStrutturaRequest(request.WubookAbilitato, request.AlloggiatiWebAbilitato, request.OsservatorioAbilitato, request.PayTouristAbilitato),
            cancellationToken);

        return Ok(new
        {
            struttura.Id,
            struttura.WubookAbilitato,
            struttura.AlloggiatiWebAbilitato,
            struttura.OsservatorioAbilitato,
            struttura.PayTouristAbilitato,
        });
    }

    [HttpPut("utenti/{utenteId:guid}/reset-password")]
    public async Task<IActionResult> ResettaPassword(Guid utenteId, [FromBody] ResettaPasswordRequest request, CancellationToken cancellationToken)
    {
        await service.ResettaPasswordAsync(currentUser, utenteId, new ApplicationSuperAdmin.ResettaPasswordRequest(request.NuovaPassword), cancellationToken);
        return NoContent();
    }

    [HttpPut("utenti/{utenteId:guid}")]
    public async Task<IActionResult> AggiornaUtente(Guid utenteId, [FromBody] AggiornaUtenteRequest request, CancellationToken cancellationToken)
    {
        var utente = await service.AggiornaUtenteAsync(
            currentUser,
            utenteId,
            new ApplicationSuperAdmin.AggiornaUtenteRequest(request.Email, request.Nome, request.Cognome, request.IsClienteAccount),
            cancellationToken);

        return Ok(new { utente.Id, utente.Email, utente.Nome, utente.Cognome, utente.IsClienteAccount });
    }

    [HttpPut("utenti/{utenteId:guid}/attivo")]
    public async Task<IActionResult> ImpostaAttivoUtente(Guid utenteId, [FromBody] ImpostaAttivoRequest request, CancellationToken cancellationToken)
    {
        var utente = await service.ImpostaAttivoUtenteAsync(currentUser, utenteId, request.Attivo, cancellationToken);
        return Ok(new { utente.Id, utente.Attivo });
    }

    /// <summary>
    /// Eliminazione DEFINITIVA di una Struttura disattivata da almeno 90 giorni — irreversibile,
    /// nessuna cancellazione automatica: va sempre invocata esplicitamente riga per riga dalla
    /// dashboard, mai da un job schedulato.
    /// </summary>
    [HttpDelete("strutture/{strutturaId:guid}")]
    public async Task<IActionResult> EliminaStruttura(Guid strutturaId, CancellationToken cancellationToken)
    {
        await service.EliminaStrutturaAsync(currentUser, strutturaId, cancellationToken);
        return NoContent();
    }

    private static DashboardSuperAdminDto ToDto(ApplicationSuperAdmin.DashboardSuperAdminInfo d) => new(
        d.Clienti.Select(c => new ClienteAdminDto(
            c.Id, c.RagioneSociale, c.PartitaIva, c.Attivo, c.CreatedAtUtc,
            c.QuotaAnnua, c.Note,
            c.NumeroUtenti, c.NumeroUtentiAttivi,
            c.Strutture.Select(s => new StrutturaAdminDto(
                s.Id, s.Nome, s.Attivo, s.DisattivataAtUtc, s.WubookAttivo, s.WubookUltimoErrore, s.ScadenzaLicenza,
                s.PoliziaStatoAttiva, s.OsservatorioAttivo, s.PayTouristAttivo,
                s.WubookAbilitato, s.AlloggiatiWebAbilitato, s.OsservatorioAbilitato, s.PayTouristAbilitato)).ToList()))
            .ToList(),
        d.Utenti.Select(u => new UtenteAdminDto(u.Id, u.Email, u.Nome, u.Cognome, u.IsSuperAdmin, u.Attivo, u.ClienteId, u.ClienteRagioneSociale, u.CreatedAtUtc, u.IsClienteAccount)).ToList());
}
