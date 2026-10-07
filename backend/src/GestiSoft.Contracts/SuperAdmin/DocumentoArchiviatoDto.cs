using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.SuperAdmin;

/// <summary>Un documento fiscale di una struttura eliminata, senza i suoi file (si scaricano a parte).</summary>
public record DocumentoArchiviatoDto(
    Guid Id,
    Guid ClienteId,
    string ClienteRagioneSociale,
    string StrutturaNome,
    string? StrutturaPartitaIva,
    TipoDocumentoArchiviato Tipo,
    string Numero,
    DateOnly Data,
    string? Controparte,
    decimal? Importo,
    bool HaPdf,
    bool HaFile,
    DateTime ArchiviatoAtUtc,
    DateOnly Scadenza);
