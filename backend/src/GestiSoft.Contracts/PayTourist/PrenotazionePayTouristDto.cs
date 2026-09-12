namespace GestiSoft.Contracts.PayTourist;

/// <param name="ScadenzaInvioUtc">Ultimo giorno utile per la trasmissione: 7 giorni dal check-out.</param>
/// <param name="InTermine">Ancora trasmissibile: a false l'invio non va offerto.</param>
public record PrenotazionePayTouristDto(
    Guid OspiteId,
    Guid? PrenotazioneId,
    string NomeOspite,
    string? Camera,
    DateTime? CheckIn,
    DateTime? CheckOut,
    bool Inviata,
    DateTime? ScadenzaInvioUtc,
    bool InTermine,
    /// <summary>Struttura PayTourist in cui la prenotazione va dichiarata, dedotta dalla tipologia della camera. Null = tipologia non associata a nessuna struttura.</summary>
    Guid? PayTouristStrutturaId,
    string? PayTouristStrutturaNome);
