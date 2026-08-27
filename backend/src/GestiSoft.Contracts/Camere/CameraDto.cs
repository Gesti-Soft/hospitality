using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Camere;

public record CameraDto(
    Guid Id,
    Guid StrutturaId,
    Guid? TipologiaId,
    string? TipologiaNome,
    StatoCamera StateRoom,
    string Nome,
    int? CapacitaOspiti,
    int? SoggiornoMinimo,
    int? IdCameraWubook,
    bool WubookAttiva);
