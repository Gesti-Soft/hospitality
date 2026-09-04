namespace GestiSoft.Contracts.Wubook;

public record WubookEventoRicevutoDto(
    Guid Id,
    string Lcode,
    int Rcode,
    bool ImportazioneRiuscita,
    string? MessaggioErrore,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
