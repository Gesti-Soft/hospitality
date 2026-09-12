namespace GestiSoft.Contracts.AlloggiatiWeb;

/// <param name="ScadenzaInvioUtc">Termine di legge per la trasmissione: 24 ore dall'arrivo, 6 se il soggiorno dura meno di 24 ore.</param>
/// <param name="SoggiornoBreve">Soggiorno sotto le 24 ore, quindi termine ridotto a 6 ore.</param>
/// <param name="InTermine">Ancora trasmissibile: a false l'interfaccia non deve offrire l'invio, perché il portale lo rifiuterebbe.</param>
public record SchedinaAlloggiatiWebDto(
    Guid OspiteId,
    Guid? PrenotazioneId,
    string NomeOspite,
    string? Camera,
    DateTime? CheckIn,
    DateTime? CheckOut,
    bool Inviata,
    DateTime? ScadenzaInvioUtc,
    bool SoggiornoBreve,
    bool InTermine);
