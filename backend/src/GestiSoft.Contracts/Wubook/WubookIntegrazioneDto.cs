namespace GestiSoft.Contracts.Wubook;

/// <summary>GestisoftToken non è mai incluso nella risposta (segreto) — solo se è configurato o meno.</summary>
public record WubookIntegrazioneDto(
    Guid StrutturaId,
    bool Attivo,
    string? GestisoftUsername,
    bool LicenzaConfigurata,
    bool CredenzialiPronte,
    DateTime? CacheAggiornataAtUtc,
    string? UltimoErrore);
