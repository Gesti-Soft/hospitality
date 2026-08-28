using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Logging;

public record LogEventoFiltro(Guid? ClienteId, Guid? StrutturaId, LivelloLog? Livello, string? Categoria = null, int Page = 1, int PageSize = 50);
