using System.Text.RegularExpressions;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public partial class LogEventoRepository(GestiSoftDbContext db) : ILogEventoRepository
{
    private static readonly TimeZoneInfo FusoItaliano = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    [GeneratedRegex(@"^(\d{1,2})(?:[/\-.](\d{1,2})(?:[/\-.](\d{2,4}))?)?$")]
    private static partial Regex RegexData();

    /// <summary>
    /// "08/05" (o "08/05/2026", "08-05-26", "08.05", o solo "08") nella ricerca testuale del log deve
    /// trovare gli eventi di quel giorno — richiesta esplicita dell'utente, che ha segnalato non
    /// funzionare anche con il solo giorno ("08" da solo, mese/anno assenti). Mese/anno assenti:
    /// si assume il mese/anno correnti (con la conservazione di 6-12 mesi introdotta di recente, di
    /// rado sono presenti log di più mesi/anni insieme; chi vuole un mese o anno diverso lo scrive per
    /// esteso). Il confronto va fatto sul giorno di calendario italiano, non UTC: CreatedAtUtc va
    /// convertito con TimeZoneInfo.ConvertTimeToUtc PRIMA di comporre la query (calcolo in C#, mai
    /// tradotto in SQL), così l'ora legale/solare è gestita correttamente senza bisogno di
    /// "AT TIME ZONE" lato Postgres.
    /// </summary>
    internal static bool ProvaEstraiIntervalloData(string testo, out DateTime inizioUtc, out DateTime fineUtc)
    {
        inizioUtc = default;
        fineUtc = default;

        var match = RegexData().Match(testo.Trim());
        if (!match.Success)
        {
            return false;
        }

        var oggiLocale = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoItaliano);
        var giorno = int.Parse(match.Groups[1].Value);
        var mese = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : oggiLocale.Month;
        var anno = match.Groups[3].Success ? NormalizzaAnno(match.Groups[3].Value) : oggiLocale.Year;

        DateTime giornoLocale;
        try
        {
            giornoLocale = new DateTime(anno, mese, giorno, 0, 0, 0, DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        inizioUtc = TimeZoneInfo.ConvertTimeToUtc(giornoLocale, FusoItaliano);
        fineUtc = TimeZoneInfo.ConvertTimeToUtc(giornoLocale.AddDays(1), FusoItaliano);
        return true;
    }

    private static int NormalizzaAnno(string testo) => testo.Length <= 2 ? 2000 + int.Parse(testo) : int.Parse(testo);

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

        if (!string.IsNullOrWhiteSpace(filtro.Ricerca))
        {
            var pattern = $"%{filtro.Ricerca.Trim()}%";

            query = ProvaEstraiIntervalloData(filtro.Ricerca, out var inizioUtc, out var fineUtc)
                ? query.Where(l =>
                    EF.Functions.ILike(l.Messaggio, pattern) ||
                    (l.Operatore != null && EF.Functions.ILike(l.Operatore, pattern)) ||
                    (l.CorrelationId != null && EF.Functions.ILike(l.CorrelationId, pattern)) ||
                    (l.CreatedAtUtc >= inizioUtc && l.CreatedAtUtc < fineUtc))
                : query.Where(l =>
                    EF.Functions.ILike(l.Messaggio, pattern) ||
                    (l.Operatore != null && EF.Functions.ILike(l.Operatore, pattern)) ||
                    (l.CorrelationId != null && EF.Functions.ILike(l.CorrelationId, pattern)));
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
