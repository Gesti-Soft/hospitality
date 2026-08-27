using GestiSoft.Application.Ospiti;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class DatiAziendaliComuneRepository(GestiSoftDbContext db) : IDatiAziendaliComuneRepository
{
    public Task<string?> GetComuneAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        db.DatiAziendali.AsNoTracking()
            .Where(d => d.StrutturaId == strutturaId)
            .Select(d => d.Comune)
            .FirstOrDefaultAsync(cancellationToken);
}
