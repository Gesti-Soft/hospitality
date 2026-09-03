namespace GestiSoft.Contracts.Osservatorio;

/// <summary>Password non è mai inclusa nella risposta (segreta) — solo se le credenziali sono configurate o meno.</summary>
public record OsservatorioAppartamentoDto(
    Guid Id,
    Guid StrutturaId,
    string? Nome,
    string? EntityCode,
    string? HotelCode,
    bool CredenzialiConfigurate,
    IReadOnlyList<Guid> TipologieIds,
    DateTime? CursoreDataAtUtc,
    DateTime? UltimoInvioAtUtc,
    int? UltimeSchedineInviate,
    string? UltimoErrore,
    DateTime? UltimaVerificaOkAtUtc);
