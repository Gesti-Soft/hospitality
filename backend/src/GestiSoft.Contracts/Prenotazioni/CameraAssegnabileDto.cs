namespace GestiSoft.Contracts.Prenotazioni;

/// <summary>Camera che l'assegnazione automatica sceglierebbe per le date del form; entrambi null se la tipologia è piena.</summary>
public record CameraAssegnabileDto(Guid? CameraId, string? NomeCamera);
