namespace GestiSoft.Contracts.Prenotazioni;

/// <summary>Esito del controllo live di sovrapposizione mostrato nel form prenotazione, prima del salvataggio.</summary>
public record DisponibilitaCameraDto(
    bool Disponibile,
    string? NumeroPrenotazione,
    string? OspiteNome,
    string? OspiteCognome,
    DateTime? CheckIn,
    DateTime? CheckOut);
