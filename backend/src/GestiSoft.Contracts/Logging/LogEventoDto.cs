using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Logging;

public record LogEventoDto(
    Guid Id,
    Guid? ClienteId,
    Guid? StrutturaId,
    LivelloLog Livello,
    string Messaggio,
    string? Dettaglio,
    string? CorrelationId,
    string Origine,
    DateTime CreatedAtUtc,
    string? Categoria,
    string? Operatore);

public record PagedResultDto<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
