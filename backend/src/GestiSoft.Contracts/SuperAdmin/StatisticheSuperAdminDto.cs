using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.SuperAdmin;

public record StatisticheSuperAdminDto(
    PanoramicaBusinessDto Panoramica,
    IReadOnlyList<TrendMensileDto> NuoviClientiPerMese,
    IReadOnlyList<IncassoRinnovoMensileDto> IncassiRinnoviPerMese,
    IReadOnlyList<LicenzaScadutaDto> LicenzeScadute,
    IReadOnlyList<LicenzaInScadenzaDto> LicenzeInScadenza,
    IReadOnlyList<SaluteIntegrazioneStrutturaDto> SaluteIntegrazioni);

public record PanoramicaBusinessDto(int ClientiAttivi, int ClientiTotali, int StruttureAttive, int StruttureTotali);

public record TrendMensileDto(int Mese, int Conteggio);

public record IncassoRinnovoMensileDto(int Mese, decimal Importo);

public record LicenzaScadutaDto(Guid StrutturaId, string NomeStruttura, string RagioneSocialeCliente, DateTime? Scadenza);

public record LicenzaInScadenzaDto(Guid StrutturaId, string NomeStruttura, string RagioneSocialeCliente, DateTime Scadenza, int GiorniRimanenti);

public record EsitoIntegrazioneDto(EsitoIntegrazione Stato, DateTime? UltimoInvioAtUtc, string? UltimoErrore);

public record SaluteIntegrazioneStrutturaDto(
    Guid StrutturaId,
    string NomeStruttura,
    string RagioneSocialeCliente,
    EsitoIntegrazioneDto AlloggiatiWeb,
    EsitoIntegrazioneDto Osservatorio,
    EsitoIntegrazioneDto PayTourist,
    EsitoIntegrazioneDto Wubook);
