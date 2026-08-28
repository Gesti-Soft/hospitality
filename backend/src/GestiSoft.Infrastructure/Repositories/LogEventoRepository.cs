using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
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
            query = query.Where(l => l.StrutturaId == strutturaId);
        }

        if (filtro.Livello is { } livello)
        {
            query = query.Where(l => l.Livello == livello);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Categoria))
        {
            query = query.Where(l => l.Categoria == filtro.Categoria);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
