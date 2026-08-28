using GestiSoft.Application.Riferimenti;
using GestiSoft.Domain.Entities.Riferimenti;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class RiferimentiRepository(GestiSoftDbContext db) : IRiferimentiRepository
{
    public async Task<IReadOnlyList<Stato>> ListStatiAsync(CancellationToken cancellationToken) =>
        await db.Stati.AsNoTracking()
            .OrderBy(s => s.Descrizione)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Comune>> CercaComuniAsync(string? ricerca, int limite, CancellationToken cancellationToken)
    {
        var query = db.Comuni.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(ricerca))
        {
            var pattern = $"%{ricerca.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Descrizione, pattern));
        }

        return await query
            .OrderBy(c => c.Descrizione)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Documento>> ListDocumentiAsync(CancellationToken cancellationToken) =>
        await db.DocumentiIdentita.AsNoTracking()
            .OrderBy(d => d.Descrizione)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TipoAlloggiato>> ListTipiAlloggiatoAsync(CancellationToken cancellationToken) =>
        await db.TipiAlloggiato.AsNoTracking()
            .OrderBy(t => t.Descrizione)
            .ToListAsync(cancellationToken);
}
