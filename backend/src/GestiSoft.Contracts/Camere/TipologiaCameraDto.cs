namespace GestiSoft.Contracts.Camere;

public record TipologiaCameraDto(
    Guid Id,
    Guid StrutturaId,
    string TipologiaCamera,
    decimal? SpesePulizia,
    decimal? Animali,
    decimal? Cauzione,
    decimal? PrezzoDefault,
    int NumeroImplementoPersona,
    decimal Implemento);
