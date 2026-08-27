using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Fatturazione;

public record DatiFatturaDto(
    Guid Id,
    Guid StrutturaId,
    Guid? DatiClienteId,
    string? ClienteNome,
    int Progressivo,
    TipoDocumentoFattura? TipoDocumento,
    RegimeFiscale? RegimeFiscale,
    int NumeroDocumento,
    DateTime DataDocumento,
    string? Divisa,
    string? Descrizione,
    decimal Quantita,
    decimal PrezzoUnitario,
    decimal PrezzoTotale,
    decimal ImportoTotale,
    AliquotaIva? AliquotaIva,
    NaturaIva? Natura,
    int Anno);
