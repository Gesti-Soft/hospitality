using GestiSoft.Domain.Entities.Riferimenti;

namespace GestiSoft.Application.Riferimenti;

public interface IRiferimentiRepository
{
    Task<IReadOnlyList<Stato>> ListStatiAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Comune>> CercaComuniAsync(string? ricerca, int limite, CancellationToken cancellationToken);

    Task<IReadOnlyList<Documento>> ListDocumentiAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TipoAlloggiato>> ListTipiAlloggiatoAsync(CancellationToken cancellationToken);
}
