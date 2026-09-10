namespace GestiSoft.Contracts.Wubook;

public record TipologiaWubookInfoDto(
    Guid TipologiaId,
    string TipologiaNome,
    int CamereCollegate,
    int? IdCameraWubook,
    bool WubookAttiva);
