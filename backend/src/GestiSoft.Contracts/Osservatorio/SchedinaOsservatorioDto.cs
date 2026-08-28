namespace GestiSoft.Contracts.Osservatorio;

public record SchedinaOsservatorioDto(
    Guid OspiteId,
    Guid? PrenotazioneId,
    string NomeOspite,
    string? Camera,
    DateTime? CheckIn,
    DateTime? CheckOut,
    bool ArrivoInviato,
    bool? PartenzaInviata);
