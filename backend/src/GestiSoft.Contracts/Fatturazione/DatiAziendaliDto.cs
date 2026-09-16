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
    /// <summary>Il logo non viaggia dentro questo DTO: si scarica a parte da /dati-aziendali/logo, cosi' ogni lettura dei dati fiscali non si porta dietro un'immagine.</summary>
    bool HaLogo,
    string? Indirizzo,
    string? NCivico,
    string? Cap,
    string? Comune,
    string? Provincia,
    string? Nazione);
