using GestiSoft.Application.Fatturazione.Sdi;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class CanaleSdiAdminRepository(GestiSoftDbContext db) : ICanaleSdiAdminRepository
{
    public async Task<IReadOnlyList<FileRicevutoSdi>> FileInQuarantenaAsync(CancellationToken cancellationToken) =>
        await SenzaByte(db.FileRicevutiSdi.Where(f =>
                f.StrutturaId == null && !f.Prova
                && (f.Esito == EsitoFileRicevutoSdi.SenzaDestinatario || f.Esito == EsitoFileRicevutoSdi.PiuDestinatari || f.Esito == EsitoFileRicevutoSdi.NonLeggibile)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FileRicevutoSdi>> FileDiCollaudoAsync(int massimo, CancellationToken cancellationToken) =>
        await SenzaByte(db.FileRicevutiSdi.Where(f => f.Prova)).Take(massimo).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RicevutaSdi>> RicevuteNonAbbinateAsync(int massimo, CancellationToken cancellationToken) =>
        await db.RicevuteSdi.AsNoTracking()
            .Where(r => r.TrasmissioneFatturaId == null)
            .OrderByDescending(r => r.RicevutaAtUtc)
            .Take(massimo)
            .Select(r => new RicevutaSdi
            {
                Id = r.Id,
                Tipo = r.Tipo,
                IdentificativoSdi = r.IdentificativoSdi,
                NomeFileFattura = r.NomeFileFattura,
                NomeFile = r.NomeFile,
                Prova = r.Prova,
                Descrizione = r.Descrizione,
                RicevutaAtUtc = r.RicevutaAtUtc,
            })
            .ToListAsync(cancellationToken);

    public Task<FileRicevutoSdi?> FileRicevutoAsync(Guid id, CancellationToken cancellationToken) =>
        db.FileRicevutiSdi.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<RicevutaSdi?> RicevutaAsync(Guid id, CancellationToken cancellationToken) =>
        db.RicevuteSdi.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    private static IQueryable<FileRicevutoSdi> SenzaByte(IQueryable<FileRicevutoSdi> query) =>
        query.AsNoTracking()
            .OrderByDescending(f => f.RicevutoAtUtc)
            .Select(f => new FileRicevutoSdi
            {
                Id = f.Id,
                IdentificativoSdi = f.IdentificativoSdi,
                Prova = f.Prova,
                NomeFile = f.NomeFile,
                IdentificativoCessionario = f.IdentificativoCessionario,
                Fornitore = f.Fornitore,
                Esito = f.Esito,
                Messaggio = f.Messaggio,
                RicevutoAtUtc = f.RicevutoAtUtc,
            });
}
