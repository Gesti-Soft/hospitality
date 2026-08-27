namespace GestiSoft.Contracts.Finanze;

public record EntrataDto(
    Guid Id,
    Guid StrutturaId,
    string? TipoEntrata,
    string? Nome,
    decimal ImportoEntrata,
    string? Descrizione,
    DateTime? Data,
    int? Anno);
