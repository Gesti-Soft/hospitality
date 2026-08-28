namespace GestiSoft.Contracts.AlloggiatiWeb;

public record SchedinaAlloggiatiWebDto(
    Guid OspiteId,
    Guid? PrenotazioneId,
    string NomeOspite,
    string? Camera,
    DateTime? CheckIn,
    DateTime? CheckOut,
    bool Inviata);
