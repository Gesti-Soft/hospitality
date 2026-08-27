namespace GestiSoft.Contracts.Finanze;

public record SpesaDto(
    Guid Id,
    Guid StrutturaId,
    string? TipoSpesa,
    string? Nome,
    decimal ImportoSpesa,
    string? Descrizione,
    string? MetodoPagamento,
    DateTime? DataSpesa,
    int? Anno);
