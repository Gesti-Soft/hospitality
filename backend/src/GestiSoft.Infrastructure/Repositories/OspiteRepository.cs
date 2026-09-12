using GestiSoft.Application.Ospiti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class OspiteRepository(GestiSoftDbContext db) : IOspiteRepository
{
    public Task<Ospite?> GetByPrenotazioneAsync(Guid prenotazioneId, CancellationToken cancellationToken) =>
        db.Ospiti.Include(o => o.Membri).FirstOrDefaultAsync(o => o.PrenotazioneId == prenotazioneId, cancellationToken);

    public Task<Ospite?> GetConPrenotazioneAsync(Guid strutturaId, Guid ospiteId, CancellationToken cancellationToken) =>
        db.Ospiti.AsNoTracking()
            .Include(o => o.Membri)
            .Include(o => o.Prenotazione).ThenInclude(p => p!.Camera)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == ospiteId && o.StrutturaId == strutturaId, cancellationToken);

    public async Task<IReadOnlyList<Ospite>> ListDaInviareAlloggiatiWebAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var oggi = DateTime.UtcNow.Date;
        var ieri = oggi.AddDays(-1);

        return await db.Ospiti.AsNoTracking()
            .Include(o => o.Membri)
            .Include(o => o.Prenotazione)
            .Where(o => o.StrutturaId == strutturaId
                && o.Prenotazione != null
                && o.Prenotazione.StatoPrenotazione == StatoPrenotazione.InCorso
                && !o.Prenotazione.StatePolice
                && o.Prenotazione.CheckIn != null
                && (o.Prenotazione.CheckIn.Value.Date == oggi || o.Prenotazione.CheckIn.Value.Date == ieri))
            .OrderBy(o => o.Prenotazione!.CheckIn)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ospite>> ListRecentiAlloggiatiWebAsync(Guid strutturaId, int anno, CancellationToken cancellationToken) =>
        await db.Ospiti.AsNoTracking()
            .Include(o => o.Membri)
            .Include(o => o.Prenotazione).ThenInclude(p => p!.Camera)
            .Where(o => o.StrutturaId == strutturaId
                && o.Prenotazione != null
                // Solo prenotazioni già arrivate al check-in (InCorso) o già concluse (Completata) —
                // non ancora "Incompleta" (check-in futuro non ancora effettuato): un check-in mai
                // avvenuto non deve comparire come "da inviare".
                && (o.Prenotazione.StatoPrenotazione == StatoPrenotazione.InCorso || o.Prenotazione.StatoPrenotazione == StatoPrenotazione.Completata)
                && o.Prenotazione.Anno == anno)
            .OrderByDescending(o => o.Prenotazione!.CheckIn)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Ospite>> ListArriviOsservatorioAsync(Guid strutturaId, IReadOnlyCollection<Guid>? tipologieIds, DateTime data, CancellationToken cancellationToken)
    {
        var giorno = data.Date;

        return await db.Ospiti.AsNoTracking()
            .Include(o => o.Membri)
            .Include(o => o.Prenotazione).ThenInclude(p => p!.Camera)
            .Where(o => o.StrutturaId == strutturaId
                && o.Prenotazione != null
                && o.Prenotazione.StatoPrenotazione == StatoPrenotazione.InCorso
                && !o.Prenotazione.PMS
                && o.Prenotazione.CheckIn != null
                && o.Prenotazione.CheckIn.Value.Date == giorno
                && (tipologieIds == null
                    || (o.Prenotazione.Camera != null
                        && o.Prenotazione.Camera.TipologiaId != null
                        && tipologieIds.Contains(o.Prenotazione.Camera.TipologiaId.Value))))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ospite>> ListCheckoutOsservatorioAsync(Guid strutturaId, IReadOnlyCollection<Guid>? tipologieIds, DateTime data, CancellationToken cancellationToken)
    {
        var giorno = data.Date;

        return await db.Ospiti.AsNoTracking()
            .Include(o => o.Membri)
            .Include(o => o.Prenotazione).ThenInclude(p => p!.Camera)
            .Where(o => o.StrutturaId == strutturaId
                && o.Prenotazione != null
                && o.Prenotazione.PMS
                && o.Prenotazione.CheckOut != null
                && o.Prenotazione.CheckOut.Value.Date == giorno
                && (tipologieIds == null
                    || (o.Prenotazione.Camera != null
                        && o.Prenotazione.Camera.TipologiaId != null
                        && tipologieIds.Contains(o.Prenotazione.Camera.TipologiaId.Value))))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ospite>> ListRecentiOsservatorioAsync(Guid strutturaId, IReadOnlyCollection<Guid>? tipologieIds, int anno, CancellationToken cancellationToken) =>
        await db.Ospiti.AsNoTracking()
            .Include(o => o.Membri)
            .Include(o => o.Prenotazione).ThenInclude(p => p!.Camera)
            .Where(o => o.StrutturaId == strutturaId
                && o.Prenotazione != null
                // Solo prenotazioni già arrivate al check-in (InCorso) o già concluse (Completata) —
                // non ancora "Incompleta" (check-in futuro non ancora effettuato): bug reale segnalato
                // dall'utente, un check-in mai avvenuto compariva come "arrivo da inviare".
                && (o.Prenotazione.StatoPrenotazione == StatoPrenotazione.InCorso || o.Prenotazione.StatoPrenotazione == StatoPrenotazione.Completata)
                && o.Prenotazione.Anno == anno
                && (tipologieIds == null
                    || (o.Prenotazione.Camera != null
                        && o.Prenotazione.Camera.TipologiaId != null
                        && tipologieIds.Contains(o.Prenotazione.Camera.TipologiaId.Value))))
            .OrderByDescending(o => o.Prenotazione!.CheckIn)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Ospite>> ListDaInviarePayTouristAsync(Guid strutturaId, IReadOnlyCollection<Guid>? tipologieIds, CancellationToken cancellationToken)
    {
        var settegiorniFa = DateTime.UtcNow.Date.AddDays(-7);

        return await db.Ospiti.AsNoTracking()
            .Include(o => o.Membri)
            .Include(o => o.Prenotazione).ThenInclude(p => p!.Camera)
            .Where(o => o.StrutturaId == strutturaId
                && o.Prenotazione != null
                && o.Prenotazione.StatoPrenotazione == StatoPrenotazione.Completata
                && !o.Prenotazione.PayTourist
                && o.Prenotazione.CheckOut != null
                && o.Prenotazione.CheckOut.Value.Date >= settegiorniFa
                && (tipologieIds == null
                    || (o.Prenotazione.Camera != null
                        && o.Prenotazione.Camera.TipologiaId != null
                        && tipologieIds.Contains(o.Prenotazione.Camera.TipologiaId.Value))))
            .OrderByDescending(o => o.Prenotazione!.CheckOut)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ospite>> ListRecentiPayTouristAsync(Guid strutturaId, IReadOnlyCollection<Guid>? tipologieIds, int anno, CancellationToken cancellationToken) =>
        await db.Ospiti.AsNoTracking()
            .Include(o => o.Membri)
            .Include(o => o.Prenotazione).ThenInclude(p => p!.Camera)
            .Where(o => o.StrutturaId == strutturaId
                && o.Prenotazione != null
                && o.Prenotazione.StatoPrenotazione == StatoPrenotazione.Completata
                && o.Prenotazione.Anno == anno
                && (tipologieIds == null
                    || (o.Prenotazione.Camera != null
                        && o.Prenotazione.Camera.TipologiaId != null
                        && tipologieIds.Contains(o.Prenotazione.Camera.TipologiaId.Value))))
            .OrderByDescending(o => o.Prenotazione!.CheckOut)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public void Add(Ospite entity) => db.Ospiti.Add(entity);

    public void AddMembro(OspiteRiga riga) => db.OspitiRighe.Add(riga);

    public void RemoveMembro(OspiteRiga riga) => db.OspitiRighe.Remove(riga);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
