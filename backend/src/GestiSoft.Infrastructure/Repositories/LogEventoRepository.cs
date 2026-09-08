using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class LogEventoRepository(GestiSoftDbContext db) : ILogEventoRepository
{
    public async Task AddAsync(LogEvento evento, CancellationToken cancellationToken)
    {
        db.LogEventi.Add(evento);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<LogEvento> Items, int TotalCount)> SearchAsync(LogEventoFiltro filtro, CancellationToken cancellationToken)
    {
        var query = db.LogEventi.AsNoTracking().AsQueryable();

        if (filtro.ClienteId is { } clienteId)
        {
            query = query.Where(l => l.ClienteId == clienteId);
        }

        if (filtro.StrutturaId is { } strutturaId)
        {
            // Include anche le righe non legate a una struttura specifica (es. modifica di un
            // utente, che può avere accesso a più strutture): altrimenti sparirebbero del tutto
            // filtrando per la struttura correntemente selezionata in UI.
            query = query.Where(l => l.StrutturaId == strutturaId || l.StrutturaId == null);
        }

        if (filtro.Livello is { } livello)
        {
            query = query.Where(l => l.Livello == livello);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Categoria))
        {
            query = query.Where(l => l.Categoria == filtro.Categoria);
        }

        if (filtro.CategorieVisibili is { } categorieVisibili)
        {
            query = query.Where(l => l.Categoria != null && categorieVisibili.Contains(l.Categoria));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<int> EliminaPrecedentiAsync(DateTime sogliaInfoUtc, DateTime sogliaAltriUtc, CancellationToken cancellationToken) =>
        db.LogEventi
            .Where(l =>
                (l.Livello == LivelloLog.Info && l.CreatedAtUtc < sogliaInfoUtc) ||
                (l.Livello != LivelloLog.Info && l.CreatedAtUtc < sogliaAltriUtc))
            .ExecuteDeleteAsync(cancellationToken);
}
