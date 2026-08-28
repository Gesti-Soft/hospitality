namespace GestiSoft.Contracts.Wubook;

public record CameraWubookInfoDto(
    Guid CameraId,
    string CameraNome,
    Guid? TipologiaId,
    string? TipologiaNome,
    int? IdCameraWubook,
    bool WubookAttiva,
    bool ChiusaOggi,
    int ChiusureCount,
    int RestrizioniCount);
