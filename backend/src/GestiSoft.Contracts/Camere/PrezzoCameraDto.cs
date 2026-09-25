namespace GestiSoft.Contracts.Camere;

public record PrezzoCameraDto(
    Guid Id,
    Guid StrutturaId,
    Guid? CameraId,
    Guid? TipologiaId,
    DateTime? DataInizio,
    DateTime? DataFine,
    decimal? PrezzoPerNotte,
    // Solo nella risposta al salvataggio: cosa è andato storto nell'invio del prezzo all'OTA.
    string? AvvisoOta = null);

/// <summary>Risposta all'eliminazione di un periodo: l'esito dell'invio all'OTA dei giorni che cambiano prezzo.</summary>
public record PeriodoPrezzoEliminatoDto(string? AvvisoOta);
