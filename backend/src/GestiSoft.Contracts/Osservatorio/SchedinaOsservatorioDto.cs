namespace GestiSoft.Contracts.Osservatorio;

/// <param name="ChiusoFinoA">Giornata fino a cui l'appartamento risulta chiuso sull'Osservatorio: gli arrivi anteriori non sono più trasmissibili.</param>
/// <param name="InTermine">Arrivo ancora trasmissibile: a false l'invio non va offerto, la giornata è già chiusa.</param>
public record SchedinaOsservatorioDto(
    Guid OspiteId,
    Guid? PrenotazioneId,
    string NomeOspite,
    string? Camera,
    DateTime? CheckIn,
    DateTime? CheckOut,
    bool ArrivoInviato,
    bool? PartenzaInviata,
    DateTime? ChiusoFinoA,
    bool InTermine,
    /// <summary>Appartamento in cui la schedina va dichiarata, dedotto dalla tipologia della camera. Null = tipologia non associata a nessun appartamento: la riga non è trasmissibile finché non viene collegata.</summary>
    Guid? AppartamentoId,
    string? AppartamentoNome);
