namespace GestiSoft.Contracts.SuperAdmin;

public record ClienteAdminDto(
    Guid Id,
    string RagioneSociale,
    string? PartitaIva,
    bool Attivo,
    DateTime CreatedAtUtc,
    decimal? QuotaMensile,
    string? Note,
    int NumeroUtenti,
    int NumeroUtentiAttivi,
    IReadOnlyList<StrutturaAdminDto> Strutture);
