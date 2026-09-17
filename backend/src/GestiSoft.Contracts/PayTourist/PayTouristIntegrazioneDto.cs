namespace GestiSoft.Contracts.PayTourist;

/// <summary>Un portale online riconosciuto da PayTourist: l'id serve a salvarlo, il nome è quello con cui si riconosce il canale di una prenotazione.</summary>
public record PayTouristPortaleOnlineDto(int Id, string Nome);

/// <summary>Token non è mai incluso nella risposta (segreto) — solo se è configurato o meno.</summary>
public record PayTouristIntegrazioneDto(
    Guid StrutturaId,
    bool TokenConfigurato,
    bool PortaleOnlineAttivo,
    IReadOnlyList<PayTouristPortaleOnlineDto> PortaliAttivi);
