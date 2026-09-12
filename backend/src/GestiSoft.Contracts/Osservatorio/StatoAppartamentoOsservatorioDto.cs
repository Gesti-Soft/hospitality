namespace GestiSoft.Contracts.Osservatorio;

/// <param name="ChiusoFinoA">Giornata che il servizio Osservatorio indica come prossima da chiudere: è quella i cui arrivi sono ancora trasmissibili.</param>
/// <param name="Errore">Motivo per cui il dato non è stato letto; in quel caso ChiusoFinoA è l'ultimo valore noto (o null).</param>
public record StatoAppartamentoOsservatorioDto(Guid AppartamentoId, string? Nome, DateTime? ChiusoFinoA, string? Errore);
