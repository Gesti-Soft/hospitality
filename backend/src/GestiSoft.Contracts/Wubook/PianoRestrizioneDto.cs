namespace GestiSoft.Contracts.Wubook;

public record RegoleRestrizioneDto(int? MinStay, int? MinStayArrival, int? MaxStay, int? MaxStayArrival, bool? Chiuso, bool? ChiusoArrivo, bool? ChiusoPartenza);

public record PianoRestrizioneDto(int Id, string Nome, RegoleRestrizioneDto? Regole);

public record CreaPianoRestrizioneRequestDto(string Nome, RegoleRestrizioneDto? Regole);

public record AggiornaPianoRestrizioneRequestDto(string? Nome, RegoleRestrizioneDto? Regole);
