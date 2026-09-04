namespace GestiSoft.Contracts.Fatturazione;

public record DatiClienteDto(
    Guid Id,
    Guid StrutturaId,
    string? Iso2,
    string? PIva,
    string? CodiceFiscale,
    string? Denominazione,
    string? Nome,
    string? Cognome,
    string? Indirizzo,
    string? NCivico,
    string? Cap,
    string? LuogoResidenza,
    string? Provincia,
    string? Cittadinanza,
    string? CodiceDestinatario,
    string? Pec,
    string? CustomerKey);

/// <summary>Esito della risoluzione find-or-create del Cliente fatturabile per una Prenotazione — vedi FatturazioneService.RisolviClientePerPrenotazioneAsync.</summary>
public record ClienteRisoltoDto(DatiClienteDto Cliente, bool AppenaCreato);
