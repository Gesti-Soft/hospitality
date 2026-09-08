using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Logging;

/// <summary>
/// <paramref name="CategorieVisibili"/>: null per il Super Admin (nessuna restrizione); per un
/// Cliente (sempre un amministratore della struttura — vedi LogController, l'accesso stesso è
/// negato a chi non lo è) è <see cref="LogVisibilita.CategorieVisibiliCliente"/> più "Auth" —
/// applicata qui a livello di query, non un filtro opzionale scelto dal chiamante: la sicurezza sta
/// nel controller che la imposta sempre in base al ruolo, mai in base a un parametro arrivato dal client.
/// </summary>
public record LogEventoFiltro(
    Guid? ClienteId,
    Guid? StrutturaId,
    LivelloLog? Livello,
    string? Categoria = null,
    string? Ricerca = null,
    int Page = 1,
    int PageSize = 50,
    IReadOnlyList<string>? CategorieVisibili = null);
