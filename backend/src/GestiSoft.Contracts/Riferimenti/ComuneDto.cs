namespace GestiSoft.Contracts.Riferimenti;

public record ComuneDto(Guid Id, long Codice, string Descrizione, string? Provincia, string? CodiceBelfiore, string? Cap);
