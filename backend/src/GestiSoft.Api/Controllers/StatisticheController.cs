using GestiSoft.Application.Auth;
using GestiSoft.Application.Statistiche;
using GestiSoft.Contracts.Statistiche;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/statistiche")]
[Authorize]
public class StatisticheController(StatisticheService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var risultato = await service.GetStatisticheAsync(currentUser, strutturaId, anno ?? DateTime.UtcNow.Year, cancellationToken);
        return Ok(ToDto(risultato));
    }

    [HttpGet("anni")]
    public async Task<IActionResult> AnniDisponibili(Guid strutturaId, CancellationToken cancellationToken)
    {
        var anni = await service.GetAnniDisponibiliAsync(currentUser, strutturaId, cancellationToken);
        return Ok(anni);
    }

    private static StatisticheStrutturaDto ToDto(StatisticheStrutturaResult r) => new(
        r.Anno,
        new StatisticheKpiDto(r.Kpi.NumeroPrenotazioni, r.Kpi.RicavoStimato, r.Kpi.RicavoEffettivo, r.Kpi.PermanenzaMediaNotti, r.Kpi.TassoOccupazionePercentuale),
        r.PrenotazioniPerAgenzia.Select(v => new VoceConteggioDto(v.Etichetta, v.Conteggio)).ToList(),
        r.PrenotazioniPerNazionalita.Select(v => new VocePercentualeDto(v.Etichetta, v.Conteggio, v.Percentuale)).ToList(),
        r.AndamentoRicavoMensile.Select(v => new ValoreMensileDto(v.Mese, v.Valore)).ToList(),
        r.RicavoPerTipologiaCamera.Select(v => new VoceImportoDto(v.Etichetta, v.Importo)).ToList(),
        new MatricePrenotazioniTipologiaDto(
            r.PrenotazioniPerTipologiaMese.Tipologie,
            r.PrenotazioniPerTipologiaMese.Righe.Select(riga => new RigaMatriceMeseDto(riga.Mese, riga.ConteggiPerTipologia)).ToList()),
        new StatisticheTassaSoggiornoDto(
            r.TassaSoggiorno.TotaleAnno,
            r.TassaSoggiorno.AndamentoMensile.Select(v => new ValoreMensileDto(v.Mese, v.Valore)).ToList()));
}
