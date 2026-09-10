namespace GestiSoft.Contracts.Wubook;

public record TipologiaWubookInfoDto(
    Guid TipologiaId,
    string TipologiaNome,
    int CamereCollegate,
    int ChiusureCount,
    int RestrizioniCount,
    int? IdCameraWubook,
    bool WubookAttiva);
