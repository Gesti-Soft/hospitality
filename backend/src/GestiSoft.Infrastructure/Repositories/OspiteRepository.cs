using GestiSoft.Application.Ospiti;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class OspiteRepository(GestiSoftDbContext db) : IOspiteRepository
{
    public Task<Ospite?> GetByPrenotazioneAsync(Guid prenotazioneId, CancellationToken cancellationToken) =>
        db.Ospiti.Include(o => o.Membri).FirstOrDefaultAsync(o => o.PrenotazioneId == prenotazioneId, cancellationToken);

    public void Add(Ospite entity) => db.Ospiti.Add(entity);

    public void RemoveMembro(OspiteRiga riga) => db.OspitiRighe.Remove(riga);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
