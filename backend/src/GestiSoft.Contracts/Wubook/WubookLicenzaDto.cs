namespace GestiSoft.Contracts.Wubook;

/// <summary>
/// Vista Super Admin — include i valori segreti, mai esposti al Cliente. Il Token Wubook non è qui:
/// è uguale per tutte le Strutture, vedi ImpostazioniGlobaliDto. La scadenza della licenza non è
/// qui: è la licenza software GestiSoft della Struttura, vedi LicenzaStrutturaDto.
/// </summary>
public record WubookLicenzaDto(
    Guid StrutturaId,
    string? GestisoftUsername,
    string? GestisoftToken,
    string? CodiceStruttura);
