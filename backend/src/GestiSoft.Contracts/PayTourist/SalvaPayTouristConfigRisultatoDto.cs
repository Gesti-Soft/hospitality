namespace GestiSoft.Contracts.PayTourist;

/// <summary>
/// Esito del salvataggio della configurazione PayTourist, con la verifica del Token eseguita subito
/// dopo (GET api/v1/structures). <c>VerificaOk</c> è nullo quando non è stato indicato nessun token
/// nuovo: in quel caso non c'era niente da verificare, e all'operatore non va mostrato nessun esito.
/// Un token rifiutato dal portale non arriva mai qui — il salvataggio viene respinto con un errore.
/// </summary>
public record SalvaPayTouristConfigRisultatoDto(PayTouristIntegrazioneDto Integrazione, bool? VerificaOk, string? VerificaErrore);
