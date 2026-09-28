using GestiSoft.Application.Pagamenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class PagamentoPrenotazioneRepository(GestiSoftDbContext db) : IPagamentoPrenotazioneRepository
{
    public async Task<IReadOnlyList<PagamentoPrenotazione>> ListByPrenotazioneAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken) =>
        await db.PagamentiPrenotazione.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId && p.PrenotazioneId == prenotazioneId)
            .OrderBy(p => p.Data)
            .ThenBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<PagamentoPrenotazione?> GetAsync(Guid strutturaId, Guid pagamentoId, CancellationToken cancellationToken) =>
        db.PagamentiPrenotazione.FirstOrDefaultAsync(p => p.StrutturaId == strutturaId && p.Id == pagamentoId, cancellationToken);

    public async Task AddAsync(PagamentoPrenotazione pagamento, CancellationToken cancellationToken)
    {
        db.PagamentiPrenotazione.Add(pagamento);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PagamentoPrenotazione pagamento, CancellationToken cancellationToken)
    {
        if (db.Entry(pagamento).State == EntityState.Detached)
        {
            db.PagamentiPrenotazione.Update(pagamento);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PagamentoPrenotazione pagamento, CancellationToken cancellationToken)
    {
        db.PagamentiPrenotazione.Remove(pagamento);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<decimal> SommaAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken) =>
        Netto(db.PagamentiPrenotazione.Where(p => p.StrutturaId == strutturaId && p.Data.Year == anno), cancellationToken);

    public Task<decimal> SommaFinoAdAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken) =>
        Netto(db.PagamentiPrenotazione.Where(p => p.StrutturaId == strutturaId && p.Data.Year <= anno), cancellationToken);

    public async Task<IReadOnlyList<int>> ListaAnniAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await db.PagamentiPrenotazione.AsNoTracking()
            .Where(p => p.StrutturaId == strutturaId)
            .Select(p => p.Data.Year)
            .Distinct()
            .ToListAsync(cancellationToken);

    private static Task<decimal> Netto(IQueryable<PagamentoPrenotazione> pagamenti, CancellationToken cancellationToken) =>
        pagamenti.SumAsync(p => p.Tipo == TipoPagamento.Rimborso ? -p.Importo : p.Importo, cancellationToken);
}
