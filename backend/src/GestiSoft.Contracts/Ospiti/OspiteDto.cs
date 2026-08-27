using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Ospiti;

public record OspiteDto(
    Guid Id,
    Guid StrutturaId,
    Guid? PrenotazioneId,
    string? TipoOspite,
    int? Permanenza,
    DateTime? DataNascita,
    Sesso? Sesso,
    string? Cognome,
    string? Nome,
    string? Cittadinanza,
    string? LuogoNascita,
    string? StatoNascita,
    string? LuogoResidenza,
    string? Email,
    string? Documento,
    string? NumeroDocumento,
    string? RilascioDocumento,
    bool EsenteDaTassa,
    IReadOnlyList<OspiteRigaDto> Membri);
