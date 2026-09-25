using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Camere;

/// <summary>Fascia d'età del supplemento per persona in più: età comprese, importo a notte in euro o in percentuale del supplemento pieno (0 = gratis).</summary>
public record FasciaEtaSupplementoDto(int EtaMin, int EtaMax, decimal ImportoPerNotte, TipoVariazionePrezzo TipoImporto = TipoVariazionePrezzo.Euro);
