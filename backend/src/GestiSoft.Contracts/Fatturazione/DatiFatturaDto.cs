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
    /// <summary>Imposta di soggiorno riaddebitata, esposta in fattura come riga esclusa art. 15 (natura N1).</summary>
    decimal? ImpostaSoggiorno,
    /// <summary>Bollo virtuale da 2 €, calcolato dal sistema sulle sole somme non soggette a IVA sopra 77,47 €.</summary>
    decimal? ImportoBollo,
    int Anno);
