namespace GestiSoft.Application.AlloggiatiWeb;

/// <summary>Una voce di una tabella di riferimento Alloggiati Web, indicizzabile per Descrizione.</summary>
public record VoceAnagrafica(string Descrizione, string Codice, string? Provincia);

/// <summary>
/// Letture mirate sulle tabelle di riferimento globali (Comune/Stato/Documento/TipoAlloggiato,
/// seedate in Fase 1) necessarie a comporre la schedina. Porta GetStatiComuni/GetDocumenti/
/// TipoAlloggiato di StartUpSettingLogic del legacy: nel legacy Comuni e Stati venivano
/// concatenati in un'unica lista (StatiComuni) perché la schedina cerca il codice di un luogo di
/// nascita/cittadinanza per Descrizione senza sapere a priori se è un comune italiano o uno stato
/// estero — stessa scelta qui in <see cref="ListLuoghiAsync"/>.
/// </summary>
public interface IAnagraficaAlloggiatiWebRepository
{
    Task<IReadOnlyList<VoceAnagrafica>> ListLuoghiAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<VoceAnagrafica>> ListDocumentiAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<VoceAnagrafica>> ListTipiAlloggiatoAsync(CancellationToken cancellationToken);
}
