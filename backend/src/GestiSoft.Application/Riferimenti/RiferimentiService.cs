using GestiSoft.Domain.Entities.Riferimenti;

namespace GestiSoft.Application.Riferimenti;

/// <summary>
/// Tabelle di riferimento condivise (Comuni/Stati/Documenti d'identità/TipoAlloggiato) usate per
/// popolare i menu a tendina della scheda ospiti (Alloggiati Web) — dati globali, non legati a una
/// Struttura, quindi nessun controllo di permesso per-struttura (solo utente autenticato).
/// </summary>
public class RiferimentiService(IRiferimentiRepository riferimenti)
{
    public Task<IReadOnlyList<Stato>> ListaStatiAsync(CancellationToken cancellationToken) =>
        riferimenti.ListStatiAsync(cancellationToken);

    public Task<IReadOnlyList<Comune>> CercaComuniAsync(string? ricerca, CancellationToken cancellationToken) =>
        riferimenti.CercaComuniAsync(ricerca, limite: 50, cancellationToken);

    public Task<IReadOnlyList<Documento>> ListaDocumentiAsync(CancellationToken cancellationToken) =>
        riferimenti.ListDocumentiAsync(cancellationToken);

    public Task<IReadOnlyList<TipoAlloggiato>> ListaTipiAlloggiatoAsync(CancellationToken cancellationToken) =>
        riferimenti.ListTipiAlloggiatoAsync(cancellationToken);
}
