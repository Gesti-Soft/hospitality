using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Ospiti;

public record OspiteRigaDto(
    Guid Id,
    Guid? CameraId,
    int? Permanenza,
    DateTime? DataNascita,
    Sesso? Sesso,
    string? Cognome,
    string? Nome,
    string? Cittadinanza,
    string? LuogoNascita,
    string? StatoNascita,
    string? LuogoResidenza,
    bool? PostoLetto,
    bool EsenteDaTassa);
