namespace GestiSoft.Contracts.Osservatorio;

/// <summary>Esito del salvataggio (creazione/modifica) di un appartamento Osservatorio, comprensivo del test di connessione eseguito subito dopo — vedi OsservatorioConfigService.VerificaConnessioneAsync.</summary>
public record SalvaOsservatorioAppartamentoRisultatoDto(OsservatorioAppartamentoDto Appartamento, bool ConnessioneOk, string? ConnessioneErrore);
