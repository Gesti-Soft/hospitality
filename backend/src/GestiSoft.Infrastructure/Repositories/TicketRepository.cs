using GestiSoft.Application.Assistenza;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class TicketRepository(GestiSoftDbContext db) : ITicketRepository
{
    private const int LimiteLista = 200;

    public async Task<IReadOnlyList<TicketRiepilogo>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var righe = await Riepiloghi(db.Ticket.AsNoTracking().Where(t => t.StrutturaId == strutturaId))
            .OrderBy(r => r.Ticket.Stato)
            .ThenByDescending(r => r.Ticket.UpdatedAtUtc ?? r.Ticket.CreatedAtUtc)
            .Take(LimiteLista)
            .ToListAsync(cancellationToken);

        return righe.Select(r => new TicketRiepilogo(r.Ticket, r.StrutturaNome, r.ClienteRagioneSociale)).ToList();
    }

    public async Task<IReadOnlyList<TicketRiepilogo>> ListTuttiAsync(bool soloAperti, CancellationToken cancellationToken)
    {
        var query = db.Ticket.AsNoTracking();
        if (soloAperti)
        {
            query = query.Where(t => t.Stato == StatoTicket.Aperto);
        }

        var righe = await Riepiloghi(query)
            .OrderBy(r => r.Ticket.Stato)
            .ThenByDescending(r => r.Ticket.UltimoMessaggioClienteAtUtc)
            .Take(LimiteLista)
            .ToListAsync(cancellationToken);

        return righe.Select(r => new TicketRiepilogo(r.Ticket, r.StrutturaNome, r.ClienteRagioneSociale)).ToList();
    }

    public async Task<TicketRiepilogo?> GetDettaglioAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await db.Ticket
            .Include(t => t.Messaggi.OrderBy(m => m.CreatedAtUtc)).ThenInclude(m => m.AutoreUtente)
            .Include(t => t.Messaggi).ThenInclude(m => m.Allegati)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        var nomi = await db.Strutture.AsNoTracking()
            .Where(s => s.Id == ticket.StrutturaId)
            .Select(s => new { s.Nome, s.Cliente!.RagioneSociale })
            .FirstOrDefaultAsync(cancellationToken);

        return new TicketRiepilogo(ticket, nomi?.Nome ?? string.Empty, nomi?.RagioneSociale ?? string.Empty);
    }

    public Task<TicketAllegato?> GetAllegatoAsync(Guid allegatoId, CancellationToken cancellationToken) =>
        db.TicketAllegati.AsNoTracking().FirstOrDefaultAsync(a => a.Id == allegatoId, cancellationToken);

    // Stessa regola di AssistenzaService.NonLettoDallaStruttura / NonLettoDalloStaff, scritta qui in
    // forma traducibile in SQL: il badge conta sul database, non su un elenco già caricato.
    public Task<int> ContaNonLettiStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        db.Ticket.CountAsync(
            t => t.StrutturaId == strutturaId
                && t.UltimoMessaggioStaffAtUtc != null
                && (t.LettoClienteAtUtc == null || t.LettoClienteAtUtc < t.UltimoMessaggioStaffAtUtc),
            cancellationToken);

    public Task<int> ContaNonLettiStaffAsync(CancellationToken cancellationToken) =>
        db.Ticket.CountAsync(
            t => t.Stato == StatoTicket.Aperto
                && (t.LettoStaffAtUtc == null || t.LettoStaffAtUtc < t.UltimoMessaggioClienteAtUtc),
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListDaAnonimizzareAsync(DateTime chiusiPrimaDi, CancellationToken cancellationToken) =>
        await db.Ticket.AsNoTracking()
            .Where(t => t.Stato == StatoTicket.Chiuso && t.ChiusoAtUtc < chiusiPrimaDi && t.AnonimizzatoAtUtc == null)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TicketAllegato>> ListAllegatiRimastiAsync(IReadOnlyCollection<Guid> ticketIds, CancellationToken cancellationToken) =>
        await db.TicketAllegati.AsNoTracking()
            .Where(a => a.EliminatoAtUtc == null && ticketIds.Contains(a.Messaggio!.TicketId))
            .ToListAsync(cancellationToken);

    // Prima i messaggi (gli allegati se ne vanno con loro, FK in cascata), poi il segno sul ticket:
    // se il secondo passo fallisce, al giro successivo il ticket risulta ancora da anonimizzare e si
    // ripete tutto, senza nulla da cancellare di nuovo. Al contrario resterebbe segnato come
    // anonimizzato con i messaggi ancora lì.
    public async Task AnonimizzaAsync(IReadOnlyCollection<Guid> ticketIds, DateTime adesso, CancellationToken cancellationToken)
    {
        await db.TicketMessaggi
            .Where(m => ticketIds.Contains(m.TicketId))
            .ExecuteDeleteAsync(cancellationToken);

        await db.Ticket
            .Where(t => ticketIds.Contains(t.Id))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.AutoreUtenteId, (Guid?)null)
                    .SetProperty(t => t.AnonimizzatoAtUtc, adesso)
                    .SetProperty(t => t.UpdatedAtUtc, adesso),
                cancellationToken);
    }

    public async Task AddAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        db.Ticket.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMessaggioAsync(TicketMessaggio messaggio, CancellationToken cancellationToken)
    {
        db.TicketMessaggi.Add(messaggio);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SalvaAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private sealed class RigaRiepilogo
    {
        public required Ticket Ticket { get; init; }

        public required string StrutturaNome { get; init; }

        public required string ClienteRagioneSociale { get; init; }
    }

    // Proiezione su una classe con inizializzatori e non direttamente sul record: EF traduce in SQL
    // l'ordinamento su r.Ticket solo se la proiezione è fatta di assegnazioni di membri.
    private IQueryable<RigaRiepilogo> Riepiloghi(IQueryable<Ticket> query) =>
        from t in query
        join s in db.Strutture on t.StrutturaId equals s.Id
        join c in db.Clienti on s.ClienteId equals c.Id
        select new RigaRiepilogo { Ticket = t, StrutturaNome = s.Nome, ClienteRagioneSociale = c.RagioneSociale };
}
