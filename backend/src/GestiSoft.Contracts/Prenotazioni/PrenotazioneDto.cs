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
