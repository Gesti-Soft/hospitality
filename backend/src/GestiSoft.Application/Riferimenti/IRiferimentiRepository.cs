using GestiSoft.Domain.Entities.Riferimenti;

namespace GestiSoft.Application.Riferimenti;

public interface IRiferimentiRepository
{
    Task<IReadOnlyList<Stato>> ListStatiAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Acronimo ISO2 dello stato con quella descrizione, confronto esatto ma case-insensitive
    /// (la scheda ospiti salva la Cittadinanza come descrizione presa da questa stessa tabella).
    /// null se la descrizione non c'è in tabella o se quella riga non ha un acronimo.
    /// </summary>
    Task<string?> GetAcronimoStatoPerDescrizioneAsync(string descrizione, CancellationToken cancellationToken);

    Task<IReadOnlyList<Comune>> CercaComuniAsync(string? ricerca, int limite, CancellationToken cancellationToken);

    Task<IReadOnlyList<Documento>> ListDocumentiAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TipoAlloggiato>> ListTipiAlloggiatoAsync(CancellationToken cancellationToken);
}
