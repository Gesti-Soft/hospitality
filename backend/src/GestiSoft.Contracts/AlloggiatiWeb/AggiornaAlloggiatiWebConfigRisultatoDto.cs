namespace GestiSoft.Contracts.AlloggiatiWeb;

/// <summary>Esito del salvataggio delle credenziali Alloggiati Web, comprensivo del test di connessione eseguito subito dopo — vedi AlloggiatiWebConfigService.VerificaConnessioneAsync.</summary>
public record AggiornaAlloggiatiWebConfigRisultatoDto(AlloggiatiWebIntegrazioneDto Integrazione, bool ConnessioneOk, string? ConnessioneErrore);
