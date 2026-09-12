namespace GestiSoft.Domain.Entities;

/// <summary>
/// Termini di legge per la trasmissione delle schedine alloggiati alla Polizia di Stato:
/// entro 24 ore dall'arrivo, ridotte a 6 ore per i soggiorni che durano meno di 24 ore.
/// Superato il termine il portale rifiuta la trasmissione, quindi una schedina fuori termine non
/// va più inviata in automatico — va registrata a mano sul portale, e per questo non deve sparire
/// in silenzio (vedi AlloggiatiWebInvioService).
///
/// Calcolo unico riusato da job, API e schermata operativa, perché una divergenza tra "cosa invia
/// il job" e "cosa mostra la pagina come inviabile" sarebbe invisibile e produrrebbe proprio gli
/// errori che questa regola serve a evitare.
/// </summary>
public static class TerminiSchedina
{
    public static readonly TimeSpan TermineOrdinario = TimeSpan.FromHours(24);

    public static readonly TimeSpan TermineSoggiornoBreve = TimeSpan.FromHours(6);

    /// <summary>
    /// Soggiorno sotto le 24 ore: check-out nello stesso giorno del check-in. Le due date sono
    /// registrate senza orario, quindi la durata reale non è ricavabile — ma "stesso giorno" è
    /// l'unico caso in cui il soggiorno è certamente inferiore alle 24 ore, e una notte (giorni
    /// diversi) è certamente pari o superiore.
    /// </summary>
    public static bool IsSoggiornoBreve(Prenotazione prenotazione) =>
        prenotazione.CheckIn is { } checkIn && prenotazione.CheckOut is { } checkOut && checkIn.Date == checkOut.Date;

    /// <summary>
    /// Momento da cui decorre il termine. Si preferisce sempre l'arrivo reale; se manca (prenotazioni
    /// anteriori a CheckInEffettuatoAtUtc) si usa la mezzanotte del giorno di check-in, che anticipa
    /// la scadenza invece di posticiparla: meglio considerare fuori termine una schedina che forse
    /// era ancora valida, che tentare un invio che il portale rifiuterebbe.
    /// </summary>
    public static DateTime? ArrivoUtc(Prenotazione prenotazione) =>
        prenotazione.CheckInEffettuatoAtUtc ?? (prenotazione.CheckIn is { } checkIn ? DateTime.SpecifyKind(checkIn.Date, DateTimeKind.Utc) : null);

    /// <summary>Istante entro cui la schedina deve essere trasmessa. Null se manca del tutto la data di arrivo.</summary>
    public static DateTime? ScadenzaUtc(Prenotazione prenotazione) =>
        ArrivoUtc(prenotazione) is { } arrivo
            ? arrivo + (IsSoggiornoBreve(prenotazione) ? TermineSoggiornoBreve : TermineOrdinario)
            : null;

    /// <summary>Schedina ancora trasmissibile in questo momento (termine non scaduto). Senza data di arrivo si risponde false: non c'è modo di dimostrare di essere in termine.</summary>
    public static bool IsInTermine(Prenotazione prenotazione, DateTime adessoUtc) =>
        ScadenzaUtc(prenotazione) is { } scadenza && adessoUtc <= scadenza;
}
