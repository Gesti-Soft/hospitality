using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.SuperAdmin;

/// <summary>Il canale SdI di GestiSoft e quello che aspetta il Super Admin.</summary>
public record CanaleSdiDto(
    /// <summary>Codice destinatario del canale in ricezione. Null finché lo SdI non l'ha assegnato.</summary>
    string? CodiceDestinatario,
    string? CodiceFiscaleTrasmittente,
    /// <summary>Si spedisce allo SdI di collaudo: le fatture inviate non contano.</summary>
    bool Collaudo,
    /// <summary>Ambiente di test: gli esiti del collaudo cambiano lo stato delle fatture, come in produzione.</summary>
    bool CollaudoApplicaEsiti,
    IReadOnlyList<FileRicevutoSdiDto> Quarantena,
    IReadOnlyList<RicevutaSdiDto> RicevuteNonAbbinate,
    IReadOnlyList<FileRicevutoSdiDto> RicezioniDiCollaudo);

public record FileRicevutoSdiDto(
    Guid Id,
    string IdentificativoSdi,
    string NomeFile,
    string? Fornitore,
    /// <summary>Partita IVA o codice fiscale del destinatario scritti nella fattura.</summary>
    string? IdentificativoCessionario,
    EsitoFileRicevutoSdi Esito,
    string? Messaggio,
    DateTime RicevutoAtUtc);

public record RicevutaSdiDto(
    Guid Id,
    TipoRicevutaSdi Tipo,
    string? IdentificativoSdi,
    string? NomeFileFattura,
    bool Prova,
    string? Descrizione,
    DateTime RicevutaAtUtc);

public record AttribuisciFileRicevutoRequest(Guid StrutturaId);
