using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class TrasmissioneFatturaRepository(GestiSoftDbContext db) : ITrasmissioneFatturaRepository
{
    public Task<TrasmissioneFattura?> GetUltimaAsync(Guid fatturaId, CancellationToken cancellationToken) =>
        db.TrasmissioniFattura
            .Where(t => t.DatiFatturaId == fatturaId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> FattureConXmlScaricatoAsync(Guid strutturaId, IReadOnlyCollection<Guid> fatturaIds, CancellationToken cancellationToken) =>
        (await db.TrasmissioniFattura.AsNoTracking()
            .Where(t => t.StrutturaId == strutturaId && !t.ViaCanale && fatturaIds.Contains(t.DatiFatturaId))
            .Select(t => t.DatiFatturaId)
            .Distinct()
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task<IReadOnlyList<TrasmissioneFattura>> ListByFatturaAsync(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken) =>
        await db.TrasmissioniFattura.AsNoTracking()
            .Where(t => t.StrutturaId == strutturaId && t.DatiFatturaId == fatturaId)
            .OrderByDescending(t => t.CreatedAtUtc)
            // I byte del file servono solo a chi lo scarica: l'elenco non se li porta dietro.
            .Select(t => new TrasmissioneFattura
            {
                Id = t.Id,
                StrutturaId = t.StrutturaId,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc,
                DatiFatturaId = t.DatiFatturaId,
                NomeFile = t.NomeFile,
                ProgressivoInvio = t.ProgressivoInvio,
                CodiceTrasmittente = t.CodiceTrasmittente,
                ViaCanale = t.ViaCanale,
                Prova = t.Prova,
                Esito = t.Esito,
                IdentificativoSdi = t.IdentificativoSdi,
                InviataAtUtc = t.InviataAtUtc,
                EsitoAtUtc = t.EsitoAtUtc,
                Nota = t.Nota,
                InviataDa = t.InviataDa,
            })
            .ToListAsync(cancellationToken);

    public Task<TrasmissioneFattura?> GetAsync(Guid strutturaId, Guid trasmissioneId, CancellationToken cancellationToken) =>
        db.TrasmissioniFattura.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trasmissioneId && t.StrutturaId == strutturaId, cancellationToken);

    public async Task<IReadOnlyList<RicevutaSdi>> RicevuteByFatturaAsync(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken) =>
        await db.RicevuteSdi.AsNoTracking()
            .Where(r => r.StrutturaId == strutturaId
                && db.TrasmissioniFattura.Any(t => t.Id == r.TrasmissioneFatturaId && t.StrutturaId == strutturaId && t.DatiFatturaId == fatturaId))
            .OrderBy(r => r.RicevutaAtUtc)
            // Come per i file: i byte si leggono solo quando qualcuno scarica la ricevuta.
            .Select(r => new RicevutaSdi
            {
                Id = r.Id,
                StrutturaId = r.StrutturaId,
                TrasmissioneFatturaId = r.TrasmissioneFatturaId,
                Tipo = r.Tipo,
                IdentificativoSdi = r.IdentificativoSdi,
                NomeFile = r.NomeFile,
                Prova = r.Prova,
                RicevutaAtUtc = r.RicevutaAtUtc,
            })
            .ToListAsync(cancellationToken);

    public Task<RicevutaSdi?> GetRicevutaAsync(Guid strutturaId, Guid ricevutaId, CancellationToken cancellationToken) =>
        db.RicevuteSdi.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == ricevutaId && r.StrutturaId == strutturaId, cancellationToken);

    public async Task AddAsync(TrasmissioneFattura trasmissione, CancellationToken cancellationToken)
    {
        db.TrasmissioniFattura.Add(trasmissione);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SalvaAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// Un'unica istruzione: o crea il contatore col valore iniziale, o lo incrementa. L'UPDATE prende
    /// il lock sulla riga, quindi due chiamate contemporanee escono con due numeri diversi, e il numero
    /// è già scritto quando lo si legge: un file che poi non parte lascia solo un buco.
    /// </summary>
    public async Task<int> ProssimoProgressivoInvioAsync(string codiceFiscale, int valoreIniziale, CancellationToken cancellationToken)
    {
        var risultato = await db.Database.SqlQuery<int>($"""
            insert into contatori_progressivo_invio ("CodiceFiscale", "UltimoProgressivo", "UpdatedAtUtc")
            values ({codiceFiscale}, {valoreIniziale}, now() at time zone 'utc')
            on conflict ("CodiceFiscale") do update
                set "UltimoProgressivo" = contatori_progressivo_invio."UltimoProgressivo" + 1,
                    "UpdatedAtUtc" = now() at time zone 'utc'
            returning "UltimoProgressivo" as "Value"
            """).ToListAsync(cancellationToken);

        return risultato.Single();
    }
}
