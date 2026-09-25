using GestiSoft.Application.Notifiche;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class NotificaRepository(GestiSoftDbContext db) : INotificaRepository
{
    private const int LimiteLista = 100;

    public Task<Notifica?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Notifiche.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Notifica>> ListaAsync(Guid strutturaId, bool soloNonLette, bool vedeStatoCamera, CancellationToken cancellationToken)
    {
        var query = Visibili(strutturaId, vedeStatoCamera).AsNoTracking();

        if (soloNonLette)
        {
            query = query.Where(n => n.LettaAtUtc == null);
        }

        return await query.OrderByDescending(n => n.CreatedAtUtc).Take(LimiteLista).ToListAsync(cancellationToken);
    }

    public Task<int> ContaNonLetteAsync(Guid strutturaId, bool vedeStatoCamera, CancellationToken cancellationToken) =>
        Visibili(strutturaId, vedeStatoCamera).CountAsync(n => n.LettaAtUtc == null, cancellationToken);

    // Confermate della struttura, senza quelle riservate a chi ha "Stato camera" se l'utente non ce l'ha.
    private IQueryable<Notifica> Visibili(Guid strutturaId, bool vedeStatoCamera) =>
        db.Notifiche.Where(n => n.StrutturaId == strutturaId
            && n.Stato == StatoNotifica.Confermata
            && (vedeStatoCamera || !n.RichiedeStatoCamera));

    public Task<bool> EsisteChiaveDedupAsync(Guid strutturaId, string chiaveDedup, CancellationToken cancellationToken) =>
        db.Notifiche.AnyAsync(n => n.StrutturaId == strutturaId && n.ChiaveDedup == chiaveDedup, cancellationToken);

    public Task<bool> EsistePerPrenotazioneAsync(Guid strutturaId, TipoNotifica tipo, Guid prenotazioneId, CancellationToken cancellationToken) =>
        db.Notifiche.AnyAsync(n => n.StrutturaId == strutturaId && n.Tipo == tipo && n.PrenotazioneId == prenotazioneId, cancellationToken);

    public Task<bool> EsisteArrivoPerPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken) =>
        db.Notifiche.AnyAsync(
            n => n.StrutturaId == strutturaId
                && n.PrenotazioneId == prenotazioneId
                && (n.Tipo == TipoNotifica.NuovaPrenotazione || n.Tipo == TipoNotifica.PrenotazioneModificata),
            cancellationToken);

    public async Task<IReadOnlyList<Notifica>> ListCancellazioniPendentiAsync(Guid strutturaId, string canale, CancellationToken cancellationToken) =>
        await db.Notifiche
            .Where(n => n.StrutturaId == strutturaId
                && n.Tipo == TipoNotifica.PrenotazioneAnnullata
                && n.Stato == StatoNotifica.InAttesa
                && n.Canale == canale
                && n.ScadenzaAttesaUtc != null && n.ScadenzaAttesaUtc > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notifica>> ListCancellazioniPendentiScaduteAsync(Guid strutturaId, DateTime adesso, CancellationToken cancellationToken) =>
        await db.Notifiche
            .Where(n => n.StrutturaId == strutturaId
                && n.Stato == StatoNotifica.InAttesa
                && n.ScadenzaAttesaUtc != null && n.ScadenzaAttesaUtc <= adesso)
            .ToListAsync(cancellationToken);

    public async Task SegnaTutteLetteAsync(Guid strutturaId, bool vedeStatoCamera, CancellationToken cancellationToken)
    {
        // Solo quelle che l'utente vede: "Segna tutte come lette" di un contabile non deve spegnere
        // l'avviso delle pulizie a chi le fa.
        var nonLette = await Visibili(strutturaId, vedeStatoCamera)
            .Where(n => n.LettaAtUtc == null)
            .ToListAsync(cancellationToken);

        var adesso = DateTime.UtcNow;
        foreach (var notifica in nonLette)
        {
            notifica.LettaAtUtc = adesso;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SegnaLettePerPrenotazioneAsync(Guid strutturaId, TipoNotifica tipo, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var esistenti = await db.Notifiche
            .Where(n => n.StrutturaId == strutturaId && n.Tipo == tipo && n.PrenotazioneId == prenotazioneId && n.LettaAtUtc == null)
            .ToListAsync(cancellationToken);

        var adesso = DateTime.UtcNow;
        foreach (var notifica in esistenti)
        {
            notifica.LettaAtUtc = adesso;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(Notifica notifica, CancellationToken cancellationToken)
    {
        db.Notifiche.Add(notifica);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Notifica notifica, CancellationToken cancellationToken)
    {
        if (db.Entry(notifica).State == EntityState.Detached)
        {
            db.Notifiche.Update(notifica);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
