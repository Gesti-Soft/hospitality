using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Camere;

/// <summary>
/// Come varia il prezzo della tipologia con il numero di ospiti, fuori dal form generale: riduzione
/// per ogni ospite in meno (null o 0 = nessuna) e unità del supplemento per ospite in più, il cui
/// importo resta nel form generale.
/// </summary>
public record PrezziOccupazioneDto(decimal? Riduzione, TipoVariazionePrezzo TipoRiduzione, TipoVariazionePrezzo TipoSupplemento = TipoVariazionePrezzo.Euro);
