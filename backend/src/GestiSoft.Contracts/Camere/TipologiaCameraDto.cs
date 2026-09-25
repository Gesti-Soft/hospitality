using GestiSoft.Domain.Enums;

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
    decimal Implemento,
    int? IdCameraWubook,
    bool WubookAttiva,
    string? CodiceCameraWubook,
    bool WubookSoloWoodoo,
    int? IntervalloPuliziaGiorni = null,
    int? IntervalloBiancheriaGiorni = null,
    // Riduzione per ogni ospite in meno rispetto agli inclusi (null = nessuna) e unità del
    // supplemento per ospite in più: si salvano con PrezziOccupazioneDto.
    decimal? RiduzioneOspiteInMeno = null,
    TipoVariazionePrezzo TipoRiduzioneOspiteInMeno = TipoVariazionePrezzo.Euro,
    TipoVariazionePrezzo TipoImplemento = TipoVariazionePrezzo.Euro);
