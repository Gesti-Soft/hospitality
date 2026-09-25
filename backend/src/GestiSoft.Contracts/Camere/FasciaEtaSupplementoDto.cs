namespace GestiSoft.Contracts.Camere;

/// <summary>Fascia d'età del supplemento per persona in più: età comprese, importo fisso a notte (0 = gratis).</summary>
public record FasciaEtaSupplementoDto(int EtaMin, int EtaMax, decimal ImportoPerNotte);
