namespace GestiSoft.Contracts.PayTourist;

public record PayTouristStrutturaDto(
    Guid Id,
    Guid StrutturaId,
    string? Nome,
    int? IdStrutturaPaytourist,
    IReadOnlyList<Guid> TipologieIds,
    DateTime? UltimoInvioAtUtc,
    int? UltimeInviate,
    string? UltimoErrore,
    DateTime? UltimaVerificaOkAtUtc);
