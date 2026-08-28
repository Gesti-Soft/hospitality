namespace GestiSoft.Contracts.Wubook;

public record PianoPrezzoDto(int Id, string Nome, bool Daily, bool IsVirtual, int? ParentId, decimal? Variazione, int? TipoVariazione);

public record CreaPianoPrezzoRequestDto(string Nome, int ParentId, int TipoVariazione, decimal Variazione);

public record AggiornaPianoPrezzoRequestDto(string? Nome, int? TipoVariazione, decimal? Variazione);
