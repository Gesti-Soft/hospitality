using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class PrenotazioneRepository(GestiSoftDbContext db) : IPrenotazioneRepository
{
    public Task<Prenotazione?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Prenotazioni.Include(p => p.Camera).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Prenotazione>> ListInArrivoAsync(Guid strutturaId, DateTime daData, CancellationToken cancellationToken)
    {
        daData = DateTime.SpecifyKind(daData, DateTimeKind.Utc);

        return await db.Prenotazioni.AsNoTracking()
            .Include(p => p.Camera)
            .Where(p => p.StrutturaId == strutturaId
                && p.StatoPrenotazione == StatoPrenotazione.Incompleta
                && p.CheckIn != null && p.CheckIn.Value.Date >= daData.Date)
            .OrderBy(p => p.CheckIn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Prenotazione>> ListInCorsoAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.Prenotazioni.AsNoTracking()
            .Include(p => p.Camera)
            .Where(p => p.StrutturaId == strutturaId && p.StatoPrenotazione == StatoPrenotazione.InCorso)
            .OrderBy(p => p.CheckOut)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Prenotazione>> ListStoricoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken) =>
        await db.Prenotazioni.AsNoTracking()
            .Include(p => p.Camera)
            .Where(p => p.StrutturaId == strutturaId
                && p.Anno == anno
                && (p.StatoPrenotazione == StatoPrenotazione.Annullata || p.StatoPrenotazione == StatoPrenotazione.Completata))
            .OrderBy(p => p.StatoPrenotazione == StatoPrenotazione.Annullata)
            .ThenByDescending(p => p.CheckIn)
            .ToListAsync(cancellationToken);

    public Task<bool> EsisteSovrapposizioneAsync(
        Guid strutturaId,
        Guid cameraId,
        DateTime checkIn,
        DateTime checkOut,
        Guid? escludiPrenotazioneId,
        CancellationToken cancellationToken)
    {
        checkIn = DateTime.SpecifyKind(checkIn, DateTimeKind.Utc);
        checkOut = DateTime.SpecifyKind(checkOut, DateTimeKind.Utc);

        return db.Prenotazioni.AnyAsync(
            p => p.StrutturaId == strutturaId
                && p.CameraId == cameraId
                && p.StatoPrenotazione != StatoPrenotazione.Annullata
                && p.Id != (escludiPrenotazioneId ?? Guid.Empty)
                && p.CheckIn != null && p.CheckOut != null
                && p.CheckIn.Value.Date < checkOut.Date
                && p.CheckOut.Value.Date > checkIn.Date,
            cancellationToken);
    }

    public Task<int> ContaDireteAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken) =>
        db.Prenotazioni.CountAsync(p => p.StrutturaId == strutturaId && p.Anno == anno && p.Agenzia == "Diretta", cancellationToken);

    public Task<decimal> SommaImportoPagatoAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken) =>
        db.Prenotazioni
            .Where(p => p.StrutturaId == strutturaId && p.Anno == anno && p.StatoPrenotazione != StatoPrenotazione.Annullata)
            .SumAsync(p => p.ImportoPagato ?? 0, cancellationToken);

    public async Task AddAsync(Prenotazione entity, CancellationToken cancellationToken)
    {
        db.Prenotazioni.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Prenotazione entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.Prenotazioni.Update(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
