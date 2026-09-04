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
        r.IncassiRinnoviPerMese.Select(v => new IncassoRinnovoMensileDto(v.Mese, v.Importo)).ToList(),
        r.LicenzeScadute.Select(l => new LicenzaScadutaDto(l.StrutturaId, l.NomeStruttura, l.RagioneSocialeCliente, l.Scadenza)).ToList(),
        r.LicenzeInScadenza.Select(l => new LicenzaInScadenzaDto(l.StrutturaId, l.NomeStruttura, l.RagioneSocialeCliente, l.Scadenza, l.GiorniRimanenti)).ToList(),
        r.SaluteIntegrazioni.Select(s => new SaluteIntegrazioneStrutturaDto(
            s.StrutturaId, s.NomeStruttura, s.RagioneSocialeCliente,
            ToDto(s.AlloggiatiWeb), ToDto(s.Osservatorio), ToDto(s.PayTourist), ToDto(s.Wubook))).ToList());

    private static EsitoIntegrazioneDto ToDto(ApplicationSuperAdmin.EsitoIntegrazioneResult e) =>
        new(e.Stato, e.UltimoInvioAtUtc, e.UltimoErrore);
}
