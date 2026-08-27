namespace GestiSoft.Contracts.Camere;

public record PrezzoCameraDto(
    Guid Id,
    Guid StrutturaId,
    Guid? CameraId,
    Guid? TipologiaId,
    DateTime? DataInizio,
    DateTime? DataFine,
    decimal? PrezzoPerNotte);
