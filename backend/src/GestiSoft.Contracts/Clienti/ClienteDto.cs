namespace GestiSoft.Contracts.Clienti;

public record ClienteDto(Guid Id, string RagioneSociale, string? PartitaIva, bool Attivo, DateTime CreatedAtUtc);
