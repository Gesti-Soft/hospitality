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
    TipoEmissioneDocumento TipoEmissione,
    ModalitaPagamento? ModalitaPagamento,
    int NumeroDocumento,
    DateTime DataDocumento,
    string? Divisa,
    /// <summary>Imponibile: somma delle righe, senza IVA né imposta di soggiorno.</summary>
    decimal PrezzoTotale,
    /// <summary>IVA di tutte le aliquote.</summary>
    decimal Imposta,
    decimal ImportoTotale,
    /// <summary>Imposta di soggiorno riaddebitata, esposta in fattura come riga esclusa art. 15 (natura N1).</summary>
    decimal? ImpostaSoggiorno,
    /// <summary>Bollo virtuale da 2 €, calcolato dal sistema sulle sole somme non soggette a IVA sopra 77,47 €.</summary>
    decimal? ImportoBollo,
    int Anno,
    IReadOnlyList<RigaFatturaDto> Righe,
    /// <summary>Le prenotazioni fatturate: la prima è di chi paga.</summary>
    IReadOnlyList<Guid> PrenotazioneIds);

/// <summary>Una riga del documento, o una riga proposta dalla prenotazione (Numero 0 finché non è salvata).</summary>
public record RigaFatturaDto(
    int Numero,
    string Descrizione,
    decimal Quantita,
    decimal PrezzoUnitario,
    decimal PrezzoTotale,
    AliquotaIva? AliquotaIva,
    NaturaIva? Natura,
    TipoRigaFattura Tipo,
    Guid? PrenotazioneId,
    Guid? PrenotazioneServizioId);

/// <summary>Righe proposte per fatturare le prenotazioni scelte, l'imposta di soggiorno e gli avvisi per l'operatore.</summary>
/// <summary>
/// Cosa resta da fatturare di una prenotazione: se il soggiorno è già in un documento, e i servizi
/// extra che non lo sono ancora (tipicamente addebitati dopo la fattura), con il loro importo.
/// </summary>
public record DaFatturareDto(bool SoggiornoFatturato, int ServiziDaFatturare, decimal ImportoServiziDaFatturare);

public record PropostaFatturaDto(IReadOnlyList<RigaFatturaDto> Righe, decimal ImpostaSoggiorno, IReadOnlyList<string> Avvisi);
