using GestiSoft.Application.Auth;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Contracts.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestiSoft.Api.Controllers;

[ApiController]
[Route("strutture/{strutturaId:guid}/fatture")]
[Authorize]
public class FatturazioneController(FatturazioneService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista(Guid strutturaId, [FromQuery] int? anno, CancellationToken cancellationToken)
    {
        var fatture = await service.ListaAsync(currentUser, strutturaId, anno, cancellationToken);
        return Ok(fatture.Select(ToDto));
    }

    [HttpGet("anni")]
    public async Task<IActionResult> AnniDisponibili(Guid strutturaId, CancellationToken cancellationToken)
    {
        var anni = await service.GetAnniDisponibiliAsync(currentUser, strutturaId, cancellationToken);
        return Ok(anni);
    }

    [HttpGet("{fatturaId:guid}")]
    public async Task<IActionResult> Get(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var fattura = await service.GetAsync(currentUser, strutturaId, fatturaId, cancellationToken);
        return Ok(ToDto(fattura));
    }

    /// <summary>L'eventuale fattura già generata per questa Prenotazione (404 se non ancora fatturata) — usata dalla scheda ospiti.</summary>
    [HttpGet("prenotazioni/{prenotazioneId:guid}")]
    public async Task<IActionResult> PerPrenotazione(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var fattura = await service.GetByPrenotazioneAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
        return fattura is null ? NotFound() : Ok(ToDto(fattura));
    }

    /// <summary>Cosa resta da fatturare della prenotazione (soggiorno, servizi extra addebitati dopo il documento).</summary>
    [HttpGet("prenotazioni/{prenotazioneId:guid}/da-fatturare")]
    public async Task<IActionResult> DaFatturare(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var d = await service.DaFatturareAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
        return Ok(new DaFatturareDto(d.SoggiornoFatturato, d.ServiziDaFatturare, d.ImportoServiziDaFatturare));
    }

    /// <summary>
    /// Righe proposte per fatturare una o più prenotazioni (la prima è di chi paga): il soggiorno e i
    /// servizi extra non ancora fatturati, l'imposta di soggiorno e gli avvisi. Non scrive nulla.
    /// </summary>
    [HttpGet("proposta")]
    public async Task<IActionResult> Proposta(
        Guid strutturaId,
        [FromQuery] Guid[] prenotazioneIds,
        [FromQuery] TipoEmissioneDocumento tipoEmissione = TipoEmissioneDocumento.Fattura,
        CancellationToken cancellationToken = default)
    {
        var proposta = await service.ProponiAsync(currentUser, strutturaId, prenotazioneIds, tipoEmissione, cancellationToken);
        return Ok(new PropostaFatturaDto(
            proposta.Righe.Select(r => new RigaFatturaDto(
                0, r.Descrizione ?? "", r.Quantita, r.PrezzoUnitario, CalcoloFattura.TotaleRiga(r.Quantita, r.PrezzoUnitario),
                r.AliquotaIva, r.Natura, r.Tipo, r.PrenotazioneId, r.PrenotazioneServizioId)).ToList(),
            proposta.ImpostaSoggiorno,
            proposta.Avvisi));
    }

    [HttpPost]
    public async Task<IActionResult> CreaDaPrenotazione(Guid strutturaId, [FromBody] CreaFatturaDaPrenotazioneRequest request, CancellationToken cancellationToken)
    {
        var fattura = await service.CreaDaPrenotazioneAsync(currentUser, strutturaId, request, cancellationToken);
        return Ok(ToDto(fattura));
    }

    /// <summary>
    /// Risolve (find-or-create) il Cliente fatturabile per una Prenotazione, PRIMA di creare
    /// davvero la fattura — se ne crea uno nuovo (bare-bones, solo nome/cognome/residenza/cittadinanza
    /// dalla scheda ospiti), il frontend lo segnala all'operatore per completarlo subito.
    /// </summary>
    [HttpPost("prenotazioni/{prenotazioneId:guid}/cliente")]
    public async Task<IActionResult> RisolviClientePerPrenotazione(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var (cliente, appenaCreato) = await service.RisolviClientePerPrenotazioneAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
        return Ok(new ClienteRisoltoDto(ToDtoCliente(cliente), appenaCreato));
    }

    [HttpPut("{fatturaId:guid}")]
    public async Task<IActionResult> Aggiorna(Guid strutturaId, Guid fatturaId, [FromBody] AggiornaFatturaRequest request, CancellationToken cancellationToken)
    {
        var fattura = await service.AggiornaAsync(currentUser, strutturaId, fatturaId, request, cancellationToken);
        return Ok(ToDto(fattura));
    }

    [HttpGet("{fatturaId:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var pdf = await service.GeneraPdfAsync(currentUser, strutturaId, fatturaId, cancellationToken);
        return File(pdf, "application/pdf", $"fattura-{fatturaId}.pdf");
    }

    [HttpGet("{fatturaId:guid}/xml")]
    public async Task<IActionResult> Xml(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var xml = await service.GeneraXmlSdiAsync(currentUser, strutturaId, fatturaId, cancellationToken);
        return File(xml, "application/xml", $"fattura-{fatturaId}.xml");
    }

    private static DatiFatturaDto ToDto(DatiFattura f) => new(
        f.Id, f.StrutturaId, f.DatiClienteId,
        f.Cliente is null ? null : !string.IsNullOrWhiteSpace(f.Cliente.Denominazione) ? f.Cliente.Denominazione : $"{f.Cliente.Nome} {f.Cliente.Cognome}".Trim(),
        f.Progressivo, f.TipoDocumento, f.RegimeFiscale, f.TipoEmissione, f.ModalitaPagamento, f.NumeroDocumento, f.DataDocumento, f.Divisa,
        f.PrezzoTotale, CalcoloFattura.Imposta(f.Righe), f.ImportoTotale,
        f.ImpostaSoggiorno, f.ImportoBollo, f.Anno,
        f.Righe.OrderBy(r => r.Numero)
            .Select(r => new RigaFatturaDto(r.Numero, r.Descrizione, r.Quantita, r.PrezzoUnitario, r.PrezzoTotale, r.AliquotaIva, r.Natura, r.Tipo, r.PrenotazioneId, r.PrenotazioneServizioId))
            .ToList(),
        // La prenotazione di chi paga per prima, poi le altre.
        f.Prenotazioni.Select(p => p.PrenotazioneId)
            .OrderBy(id => id == f.PrenotazioneId ? 0 : 1)
            .ToList());

    private static DatiClienteDto ToDtoCliente(DatiCliente c) => new(
        c.Id, c.StrutturaId, c.Iso2, c.PIva, c.CodiceFiscale, c.Denominazione, c.Nome, c.Cognome,
        c.DataNascita, c.Sesso, c.LuogoNascita,
        c.Indirizzo, c.NCivico, c.Cap, c.LuogoResidenza, c.Provincia, c.Cittadinanza,
        c.CodiceDestinatario, c.Pec, c.CustomerKey);
}
