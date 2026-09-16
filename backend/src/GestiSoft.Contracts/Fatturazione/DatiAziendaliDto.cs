using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Fatturazione;

public record DatiAziendaliDto(
    Guid StrutturaId,
    string? Iso2,
    string? PIva,
    string? CodiceFiscale,
    string? Denominazione,
    string? Nome,
    string? Cognome,
    RegimeFiscale? RegimeFiscale,
    AliquotaIva? AliquotaIvaDefault,
    NaturaIva? NaturaDefault,
    string? DicituraFattura,
    string? Indirizzo,
    string? NCivico,
    string? Cap,
    string? Comune,
    string? Provincia,
    string? Nazione);
