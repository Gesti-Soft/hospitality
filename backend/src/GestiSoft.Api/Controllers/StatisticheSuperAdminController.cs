using GestiSoft.Application.Auth;
using ApplicationSuperAdmin = GestiSoft.Application.SuperAdmin;
using GestiSoft.Contracts.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

/// <summary>
/// Pagina "Statistiche" del Super Admin — nessuna rotta annidata sotto strutture/{id}, stesso
/// motivo di SuperAdminController: qui si vedono tutti i Clienti/Strutture insieme.
/// </summary>
[ApiController]
[Route("super-admin/statistiche")]
[Authorize]
public class StatisticheSuperAdminController(ApplicationSuperAdmin.StatisticheSuperAdminService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var risultato = await service.GetStatisticheAsync(currentUser, anno ?? DateTime.UtcNow.Year, cancellationToken);
        return Ok(ToDto(risultato));
    }

    [HttpGet("anni")]
    public async Task<IActionResult> AnniDisponibili(CancellationToken cancellationToken)
    {
        var anni = await service.GetAnniDisponibiliAsync(currentUser, cancellationToken);
        return Ok(anni);
    }

    private static StatisticheSuperAdminDto ToDto(ApplicationSuperAdmin.StatisticheSuperAdminResult r) => new(
        new PanoramicaBusinessDto(r.Panoramica.ClientiAttivi, r.Panoramica.ClientiTotali, r.Panoramica.StruttureAttive, r.Panoramica.StruttureTotali),
        r.NuoviClientiPerMese.Select(v => new TrendMensileDto(v.Mese, v.Conteggio)).ToList(),
        r.IncassiPerCliente.Select(v => new IncassoPerClienteDto(v.ClienteId, v.RagioneSociale, v.ImportoPagatoAnno, v.NumeroStrutture)).ToList(),
        r.ClassificaStruttureFatturato.Select(ToDto).ToList(),
        r.ClassificaStruttureOccupazione.Select(ToDto).ToList(),
        r.SaluteIntegrazioni.Select(s => new SaluteIntegrazioneStrutturaDto(
            s.StrutturaId, s.NomeStruttura, s.RagioneSocialeCliente,
            ToDto(s.AlloggiatiWeb), ToDto(s.Osservatorio), ToDto(s.PayTourist), ToDto(s.Wubook))).ToList());

    private static ClassificaStrutturaDto ToDto(ApplicationSuperAdmin.ClassificaStrutturaResult c) =>
        new(c.StrutturaId, c.NomeStruttura, c.RagioneSocialeCliente, c.Valore);

    private static EsitoIntegrazioneDto ToDto(ApplicationSuperAdmin.EsitoIntegrazioneResult e) =>
        new(e.Stato, e.UltimoInvioAtUtc, e.UltimoErrore);
}
