using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Prenotazioni;

public record PrenotazioneDto(
    Guid Id,
    Guid StrutturaId,
    Guid? CameraId,
    string? CameraNome,
    Guid? TipologiaId,
    string? TipologiaNome,
    string? Agenzia,
    string? NumeroPrenotazione,
    decimal? ImportoPrenotazione,
    decimal? ImportoPagato,
    decimal? ImportoTotale,
    DateTime? CheckIn,
    DateTime? CheckOut,
    /// <summary>Orario reale dell'arrivo (null se il check-in non è stato fatto, o è anteriore a questo campo): da qui decorrono i termini della schedina alloggiati.</summary>
    DateTime? CheckInEffettuatoAtUtc,
    int? NumeroOspiti,
    bool StatePolice,
    bool PMS,
    bool PayTourist,
    int Anno,
    decimal? TotalTax,
    StatoPrenotazione? StatoPrenotazione,
    bool TassaSoggiornoAttiva,
    bool SpesePuliziaAttiva,
    bool AnimaliAttiva,
    bool CauzioneAttiva,
    string? OspiteNome,
    string? OspiteCognome);
