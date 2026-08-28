namespace GestiSoft.Contracts.Wubook;

public record ChiusuraCameraDto(Guid Id, Guid CameraId, DateTime DataInizio, DateTime DataFine, string? Motivo, int? Quantita);

public record CreaChiusuraCameraRequestDto(Guid CameraId, DateTime DataInizio, DateTime DataFine, string? Motivo, int? Quantita);
