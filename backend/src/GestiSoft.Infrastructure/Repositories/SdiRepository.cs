using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Fatturazione.Sdi;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class SdiRepository(GestiSoftDbContext db) : ISdiRepository
{
    public Task<TrasmissioneFattura?> TrasmissionePerNomeFileAsync(string nomeFile, CancellationToken cancellationToken) =>
        db.TrasmissioniFattura.FirstOrDefaultAsync(t => t.NomeFile == nomeFile, cancellationToken);

    public Task<TrasmissioneFattura?> TrasmissionePerIdentificativoAsync(string identificativoSdi, CancellationToken cancellationToken) =>
        db.TrasmissioniFattura
            .Where(t => t.IdentificativoSdi == identificativoSdi)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AggiungiRicevutaAsync(RicevutaSdi ricevuta, CancellationToken cancellationToken)
    {
        db.RicevuteSdi.Add(ricevuta);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> FileRicevutoEsisteAsync(string identificativoSdi, bool prova, CancellationToken cancellationToken) =>
        db.FileRicevutiSdi.AnyAsync(f => f.IdentificativoSdi == identificativoSdi && f.Prova == prova, cancellationToken);

    public async Task AggiungiFileRicevutoAsync(FileRicevutoSdi file, CancellationToken cancellationToken)
    {
        db.FileRicevutiSdi.Add(file);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> FatturaRicevutaEsisteAsync(IReadOnlyCollection<Guid> strutturaIds, string impronta, CancellationToken cancellationToken) =>
        db.FattureRicevute.AnyAsync(f => strutturaIds.Contains(f.StrutturaId) && f.Impronta == impronta, cancellationToken);

    public Task AggiungiFattureRicevuteAsync(IReadOnlyList<FatturaRicevuta> fatture, CancellationToken cancellationToken)
    {
        db.FattureRicevute.AddRange(fatture);
        return Task.CompletedTask;
    }

    /// <summary>
    /// I dati fiscali delle strutture attive sono poche centinaia di righe: si normalizzano in memoria,
    /// con la stessa regola usata per il file ricevuto, invece di affidarsi a come sono stati scritti.
    /// </summary>
    public async Task<IReadOnlyList<StrutturaDestinataria>> StrutturePerIdentificativoFiscaleAsync(string? partitaIva, string? codiceFiscale, CancellationToken cancellationToken)
    {
        var cercati = new[] { FileSdiService.Normalizza(partitaIva), FileSdiService.Normalizza(codiceFiscale) }
            .Where(c => c is not null)
            .ToHashSet();
        if (cercati.Count == 0)
        {
            return [];
        }

        var dati = await (from azienda in db.DatiAziendali.AsNoTracking()
                          join struttura in db.Strutture.AsNoTracking() on azienda.StrutturaId equals struttura.Id
                          where struttura.Attivo && struttura.SdiAbilitato
                          orderby struttura.CreatedAtUtc
                          select new { azienda.StrutturaId, struttura.ClienteId, azienda.PIva, azienda.CodiceFiscale })
            .ToListAsync(cancellationToken);

        return dati
            .Where(d => cercati.Contains(FileSdiService.Normalizza(d.PIva)) || cercati.Contains(FileSdiService.Normalizza(d.CodiceFiscale)))
            .Select(d => new StrutturaDestinataria(d.StrutturaId, d.ClienteId))
            .Distinct()
            .ToList();
    }

    public Task SalvaAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
