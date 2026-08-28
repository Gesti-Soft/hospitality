namespace GestiSoft.Contracts.Strutture;

public record StrutturaDto(
    Guid Id,
    Guid ClienteId,
    string Nome,
    DateTime CreatedAtUtc,
    bool WubookAbilitato,
    bool AlloggiatiWebAbilitato,
    bool OsservatorioAbilitato,
    bool PayTouristAbilitato);

public record ImpostaAttivoStrutturaRequest(bool Attivo);
