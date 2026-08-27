using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class AnagraficaAlloggiatiWebRepository(GestiSoftDbContext db) : IAnagraficaAlloggiatiWebRepository
{
    public async Task<IReadOnlyList<VoceAnagrafica>> ListLuoghiAsync(CancellationToken cancellationToken)
    {
        var comuni = await db.Comuni.AsNoTracking()
            .Select(c => new VoceAnagrafica(c.Descrizione, c.Codice.ToString(), c.Provincia))
            .ToListAsync(cancellationToken);

        var stati = await db.Stati.AsNoTracking()
            .Select(s => new VoceAnagrafica(s.Descrizione, s.Codice.ToString(), null))
            .ToListAsync(cancellationToken);

        return comuni.Concat(stati).ToList();
    }

    public async Task<IReadOnlyList<VoceAnagrafica>> ListDocumentiAsync(CancellationToken cancellationToken) =>
        await db.DocumentiIdentita.AsNoTracking()
            .Select(d => new VoceAnagrafica(d.Descrizione, d.Codice, null))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VoceAnagrafica>> ListTipiAlloggiatoAsync(CancellationToken cancellationToken) =>
        await db.TipiAlloggiato.AsNoTracking()
            .Select(t => new VoceAnagrafica(t.Descrizione, t.Codice, null))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VoceDocumentoConTypeId>> ListDocumentiConTypeIdAsync(CancellationToken cancellationToken) =>
        await db.DocumentiIdentita.AsNoTracking()
            .Select(d => new VoceDocumentoConTypeId(d.Descrizione, d.TypeId))
            .ToListAsync(cancellationToken);
}
