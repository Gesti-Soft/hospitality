using GestiSoft.Application.Servizi;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class ServizioStrutturaRepository(GestiSoftDbContext db) : IServizioStrutturaRepository
{
    public async Task<IReadOnlyList<ServizioStruttura>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.ServiziStruttura.AsNoTracking()
            .Where(s => s.StrutturaId == strutturaId && !s.Eliminato)
            .OrderBy(s => s.Nome)
            .ToListAsync(cancellationToken);

    public Task<ServizioStruttura?> GetAsync(Guid strutturaId, Guid servizioId, CancellationToken cancellationToken) =>
        db.ServiziStruttura.FirstOrDefaultAsync(s => s.StrutturaId == strutturaId && s.Id == servizioId, cancellationToken);

    public async Task UpsertAsync(ServizioStruttura entity, CancellationToken cancellationToken)
    {
        if (db.Entry(entity).State == EntityState.Detached)
        {
            db.ServiziStruttura.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrenotazioneServizio>> ListByPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken) =>
        await db.PrenotazioniServizi.AsNoTracking()
            .Where(r => r.StrutturaId == strutturaId && r.PrenotazioneId == prenotazioneId)
            .OrderBy(r => r.Dal)
            .ThenBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task SostituisciDellaPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, IReadOnlyList<PrenotazioneServizio> righe, CancellationToken cancellationToken)
    {
        var esistenti = await db.PrenotazioniServizi
            .Where(r => r.StrutturaId == strutturaId && r.PrenotazioneId == prenotazioneId)
            .ToListAsync(cancellationToken);

        // Stessa riga = stesso Id: una riga venduta tiene prezzo, origine e autore, cambiano solo quantità e date.
        foreach (var esistente in esistenti)
        {
            var nuova = righe.FirstOrDefault(r => r.Id == esistente.Id);
            if (nuova is null)
            {
                db.PrenotazioniServizi.Remove(esistente);
            }
            else if (nuova.Quantita != esistente.Quantita || nuova.Dal != esistente.Dal || nuova.Al != esistente.Al)
            {
                esistente.Quantita = nuova.Quantita;
                esistente.Dal = nuova.Dal;
                esistente.Al = nuova.Al;
                esistente.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        db.PrenotazioniServizi.AddRange(righe.Where(r => esistenti.All(e => e.Id != r.Id)));
        await db.SaveChangesAsync(cancellationToken);
    }
}
