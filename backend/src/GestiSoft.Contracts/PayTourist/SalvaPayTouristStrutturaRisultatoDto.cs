namespace GestiSoft.Contracts.PayTourist;

/// <summary>Esito del salvataggio (creazione/modifica) di una struttura PayTourist, comprensivo del test di connessione eseguito subito dopo — vedi PayTouristConfigService.VerificaConnessioneStrutturaAsync.</summary>
public record SalvaPayTouristStrutturaRisultatoDto(PayTouristStrutturaDto Struttura, bool ConnessioneOk, string? ConnessioneErrore);
