using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.SuperAdmin;

public record StatisticheSuperAdminDto(
    PanoramicaBusinessDto Panoramica,
    IReadOnlyList<TrendMensileDto> NuoviClientiPerMese,
    IReadOnlyList<IncassoPerClienteDto> IncassiPerCliente,
    IReadOnlyList<ClassificaStrutturaDto> ClassificaStruttureFatturato,
    IReadOnlyList<ClassificaStrutturaDto> ClassificaStruttureOccupazione,
    IReadOnlyList<SaluteIntegrazioneStrutturaDto> SaluteIntegrazioni);

public record PanoramicaBusinessDto(int ClientiAttivi, int ClientiTotali, int StruttureAttive, int StruttureTotali);

public record TrendMensileDto(int Mese, int Conteggio);

public record IncassoPerClienteDto(Guid ClienteId, string RagioneSociale, decimal ImportoPagatoAnno, int NumeroStrutture);

public record ClassificaStrutturaDto(Guid StrutturaId, string NomeStruttura, string RagioneSocialeCliente, decimal Valore);

public record EsitoIntegrazioneDto(EsitoIntegrazione Stato, DateTime? UltimoInvioAtUtc, string? UltimoErrore);

public record SaluteIntegrazioneStrutturaDto(
    Guid StrutturaId,
    string NomeStruttura,
    string RagioneSocialeCliente,
    EsitoIntegrazioneDto AlloggiatiWeb,
    EsitoIntegrazioneDto Osservatorio,
    EsitoIntegrazioneDto PayTourist,
    EsitoIntegrazioneDto Wubook);
