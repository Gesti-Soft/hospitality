namespace GestiSoft.Contracts.AlloggiatiWeb;

/// <summary>Password non è mai inclusa nella risposta (segreta) — solo se le credenziali sono configurate o meno.</summary>
public record AlloggiatiWebIntegrazioneDto(
    Guid StrutturaId,
    string? Utente,
    bool CredenzialiConfigurate,
    DateTime? UltimoInvioAtUtc,
    int? UltimeSchedineInviate,
    string? UltimoErrore);
