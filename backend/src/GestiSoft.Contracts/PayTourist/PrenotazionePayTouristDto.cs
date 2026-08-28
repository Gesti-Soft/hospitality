namespace GestiSoft.Contracts.PayTourist;

public record PrenotazionePayTouristDto(
    Guid OspiteId,
    Guid? PrenotazioneId,
    string NomeOspite,
    string? Camera,
    DateTime? CheckIn,
    DateTime? CheckOut,
    bool Inviata);
