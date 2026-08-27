namespace GestiSoft.Contracts.Finanze;

public record CauzioneDto(Guid Id, Guid StrutturaId, Guid PrenotazioneId, decimal? ImportoCauzione, DateTime? DataInserimento);
