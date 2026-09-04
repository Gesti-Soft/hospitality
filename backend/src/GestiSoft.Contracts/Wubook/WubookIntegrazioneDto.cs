namespace GestiSoft.Contracts.Wubook;

/// <summary>Vista Cliente/operatore — nessun valore segreto, solo se le credenziali sono pronte all'uso.</summary>
public record WubookIntegrazioneDto(
    Guid StrutturaId,
    bool Attivo,
    bool CredenzialiPronte,
    string? UltimoErrore);
