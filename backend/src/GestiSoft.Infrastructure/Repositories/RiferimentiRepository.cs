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
            var termine = ricerca.Trim();
            var pattern = $"%{termine}%";
            var termineLower = termine.ToLowerInvariant();
            query = query
                .Where(c => EF.Functions.ILike(c.Descrizione, pattern))
                // Il più vicino al termine cercato prima: posizione del match ascendente (chi inizia
                // con "ROMA" o lo contiene subito batte chi lo contiene in fondo al nome), poi il nome
                // più corto (tra due che iniziano allo stesso modo, "ROMA" batte "ROMAGNANO SESIA"),
                // infine alfabetico come tie-break finale — non più il semplice ordine alfabetico di
                // prima, che seppelliva "ROMA" sotto decine di comuni che la contengono solo di striscio.
                .OrderBy(c => c.Descrizione.ToLower().IndexOf(termineLower))
                .ThenBy(c => c.Descrizione.Length)
                .ThenBy(c => c.Descrizione);
        }
        else
        {
            query = query.OrderBy(c => c.Descrizione);
        }

        return await query
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
