namespace GestiSoft.Domain.Enums;

/// <summary>
/// Trattamento (piano pasti) offerto oltre al pernottamento. Senza trattamento la prenotazione è
/// "solo pernottamento". Corrispondono ai codici di settore BB, HB, FB e AI. L'ordine conta: un
/// valore più alto comprende i precedenti (vedi TrattamentoOta).
/// </summary>
public enum TipoTrattamento
{
    Colazione = 1,
    MezzaPensione = 2,
    PensioneCompleta = 3,
    AllInclusive = 4,
}
