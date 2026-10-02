using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class WubookEventoRicevutoRepository(GestiSoftDbContext db) : IWubookEventoRicevutoRepository
{
    public Task<WubookEventoRicevuto?> GetByRcodeAsync(Guid strutturaId, int rcode, CancellationToken cancellationToken) =>
        db.WubookEventiRicevuti.FirstOrDefaultAsync(e => e.StrutturaId == strutturaId && e.Rcode == rcode, cancellationToken);

    public async Task<IReadOnlyList<WubookEventoRicevuto>> ListByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.WubookEventiRicevuti.AsNoTracking()
            .Where(e => e.StrutturaId == strutturaId)
            .OrderByDescending(e => e.UpdatedAtUtc ?? e.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WubookEventoRicevuto>> ListDaElaborareAsync(Guid strutturaId, DateTime adessoUtc, CancellationToken cancellationToken) =>
        await db.WubookEventiRicevuti.AsNoTracking()
            .Where(e => e.StrutturaId == strutturaId && e.DaElaborare && (e.ProssimoTentativoUtc == null || e.ProssimoTentativoUtc <= adessoUtc))
            .OrderBy(e => e.UpdatedAtUtc ?? e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    // Istruzione unica: niente "leggi, poi scrivi", che lascerebbe un buco tra le due in cui la presa
    // in carico del Worker toglierebbe il segno appena rimesso. Il conflitto è sull'indice univoco
    // (StrutturaId, Rcode) di WubookEventoRicevutoConfiguration.
    public async Task SegnaDaElaborareAsync(Guid strutturaId, int rcode, string lcode, DateTime adessoUtc, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO wubook_eventi_ricevuti
                ("Id", "StrutturaId", "CreatedAtUtc", "UpdatedAtUtc", "Lcode", "Rcode", "ImportazioneRiuscita", "DaElaborare", "Tentativi")
            VALUES ({Guid.NewGuid()}, {strutturaId}, {adessoUtc}, {adessoUtc}, {lcode}, {rcode}, FALSE, TRUE, 0)
            ON CONFLICT ("StrutturaId", "Rcode") DO UPDATE SET
                "Lcode" = EXCLUDED."Lcode",
                "DaElaborare" = TRUE,
                "Tentativi" = 0,
                "ProssimoTentativoUtc" = NULL,
                "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
            """, cancellationToken);

    public async Task<bool> PrendiInCaricoAsync(Guid eventoId, CancellationToken cancellationToken) =>
        await db.WubookEventiRicevuti
            .Where(e => e.Id == eventoId && e.DaElaborare)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.DaElaborare, false), cancellationToken) == 1;

    public async Task RegistraEsitoAsync(Guid eventoId, string? errore, int tentativi, DateTime? riprovaDopoUtc, DateTime adessoUtc, CancellationToken cancellationToken)
    {
        var righe = db.WubookEventiRicevuti.Where(e => e.Id == eventoId);
        if (riprovaDopoUtc is { } riprova)
        {
            await righe.ExecuteUpdateAsync(s => s
                .SetProperty(e => e.ImportazioneRiuscita, false)
                .SetProperty(e => e.MessaggioErrore, errore)
                .SetProperty(e => e.Tentativi, tentativi)
                .SetProperty(e => e.ProssimoTentativoUtc, riprova)
                .SetProperty(e => e.DaElaborare, true)
                .SetProperty(e => e.UpdatedAtUtc, adessoUtc), cancellationToken);
            return;
        }

        await righe.ExecuteUpdateAsync(s => s
            .SetProperty(e => e.ImportazioneRiuscita, errore == null)
            .SetProperty(e => e.MessaggioErrore, errore)
            .SetProperty(e => e.Tentativi, tentativi)
            .SetProperty(e => e.UpdatedAtUtc, adessoUtc), cancellationToken);
    }

    public async Task UpsertAsync(WubookEventoRicevuto evento, CancellationToken cancellationToken)
    {
        if (db.Entry(evento).State == EntityState.Detached)
        {
            db.WubookEventiRicevuti.Add(evento);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
