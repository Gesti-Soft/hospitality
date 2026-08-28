namespace GestiSoft.Contracts.Wubook;

public record RestrizioneSoggiornoCameraDto(Guid Id, Guid CameraId, DateTime DataInizio, DateTime DataFine, int? MinStay, int? MaxStay, string? Motivo);

public record CreaRestrizioneSoggiornoCameraRequestDto(Guid CameraId, DateTime DataInizio, DateTime DataFine, int? MinStay, int? MaxStay, string? Motivo);
