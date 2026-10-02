namespace GestiSoft.Contracts.Wubook;

/// <summary>
/// Ricezione diretta delle prenotazioni OTA, per il pannello Super Admin. <c>IndirizzoCorretto</c>:
/// l'OTA ha registrato proprio l'indirizzo di questo gestionale. <c>UrlRegistrato</c> è quello che
/// l'OTA dice di avere ("questo gestionale" se è il nostro, che contiene il segreto).
/// <c>MotivoNonAttivabile</c> valorizzato = da qui non si può attivare né disattivare.
/// </summary>
public record WubookAvvisiDirettiDto(
    bool Attivi,
    bool IndirizzoCorretto,
    string? UrlRegistrato,
    string? UrlPrecedente,
    string? ErroreLettura,
    string? MotivoNonAttivabile);
