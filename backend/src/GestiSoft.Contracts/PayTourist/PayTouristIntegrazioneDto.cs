namespace GestiSoft.Contracts.PayTourist;

/// <summary>Token non è mai incluso nella risposta (segreto) — solo se è configurato o meno.</summary>
public record PayTouristIntegrazioneDto(
    Guid StrutturaId,
    bool TokenConfigurato,
    bool PortaleOnlineAttivo);
