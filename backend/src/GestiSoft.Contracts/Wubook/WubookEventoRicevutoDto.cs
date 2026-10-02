namespace GestiSoft.Contracts.Wubook;

public record WubookEventoRicevutoDto(
    Guid Id,
    string Lcode,
    int Rcode,
    bool ImportazioneRiuscita,
    string? MessaggioErrore,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    // Avviso diretto dell'OTA ancora da importare (in coda o in attesa di un nuovo tentativo).
    bool DaElaborare = false,
    int Tentativi = 0,
    DateTime? ProssimoTentativoUtc = null);
