using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Assistenza;

/// <summary>Ticket con i nomi di Struttura e Cliente già risolti, per gli elenchi e il dettaglio.</summary>
public record TicketRiepilogo(Ticket Ticket, string StrutturaNome, string ClienteRagioneSociale);

public interface ITicketRepository
{
    Task<IReadOnlyList<TicketRiepilogo>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Tutti i ticket di tutti i Clienti, per il pannello Super Admin: prima gli aperti, poi per ultimo messaggio.</summary>
    Task<IReadOnlyList<TicketRiepilogo>> ListTuttiAsync(bool soloAperti, CancellationToken cancellationToken);

    /// <summary>Ticket con messaggi, autori e allegati, tracciato per poterlo modificare.</summary>
    Task<TicketRiepilogo?> GetDettaglioAsync(Guid ticketId, CancellationToken cancellationToken);

    Task<TicketAllegato?> GetAllegatoAsync(Guid allegatoId, CancellationToken cancellationToken);

    Task<int> ContaNonLettiStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<int> ContaNonLettiStaffAsync(CancellationToken cancellationToken);

    /// <summary>Id dei ticket chiusi prima della soglia e non ancora anonimizzati.</summary>
    Task<IReadOnlyList<Guid>> ListDaAnonimizzareAsync(DateTime chiusiPrimaDi, CancellationToken cancellationToken);

    /// <summary>Allegati di questi ticket il cui file risulta ancora sul disco (cancellazione alla chiusura non riuscita).</summary>
    Task<IReadOnlyList<TicketAllegato>> ListAllegatiRimastiAsync(IReadOnlyCollection<Guid> ticketIds, CancellationToken cancellationToken);

    /// <summary>Cancella messaggi e allegati dei ticket, scollega l'autore e segna la data di anonimizzazione.</summary>
    Task AnonimizzaAsync(IReadOnlyCollection<Guid> ticketIds, DateTime adesso, CancellationToken cancellationToken);

    Task AddAsync(Ticket ticket, CancellationToken cancellationToken);

    Task AddMessaggioAsync(TicketMessaggio messaggio, CancellationToken cancellationToken);

    Task SalvaAsync(CancellationToken cancellationToken);
}
