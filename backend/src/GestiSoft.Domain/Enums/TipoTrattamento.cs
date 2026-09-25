namespace GestiSoft.Domain.Enums;

/// <summary>
/// Trattamento (piano pasti) offerto oltre al pernottamento. Senza trattamento la prenotazione è
/// "solo pernottamento". Corrispondono ai codici di settore BB, HB e FB.
/// </summary>
public enum TipoTrattamento
{
    Colazione = 1,
    MezzaPensione = 2,
    PensioneCompleta = 3,
}
