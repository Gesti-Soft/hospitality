using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Notifiche;

public interface INotificaRepository
{
    Task<Notifica?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Notifiche Confermate (mai InAttesa) di una Struttura, più recenti prima.</summary>
    Task<IReadOnlyList<Notifica>> ListaAsync(Guid strutturaId, bool soloNonLette, CancellationToken cancellationToken);

    Task<int> ContaNonLetteAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<bool> EsisteChiaveDedupAsync(Guid strutturaId, string chiaveDedup, CancellationToken cancellationToken);

    Task<bool> EsistePerPrenotazioneAsync(Guid strutturaId, TipoNotifica tipo, Guid prenotazioneId, CancellationToken cancellationToken);

    /// <summary>Cancellazioni Wubook ancora in finestra di grazia (non scadute) per una Struttura+canale — candidate per essere fuse in una modifica.</summary>
    Task<IReadOnlyList<Notifica>> ListCancellazioniPendentiAsync(Guid strutturaId, string canale, CancellationToken cancellationToken);

    /// <summary>Cancellazioni Wubook la cui finestra di grazia è scaduta senza una prenotazione corrispondente — da rendere visibili.</summary>
    Task<IReadOnlyList<Notifica>> ListCancellazioniPendentiScaduteAsync(Guid strutturaId, DateTime adesso, CancellationToken cancellationToken);

    Task SegnaTutteLetteAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task SegnaLettePerPrenotazioneAsync(Guid strutturaId, TipoNotifica tipo, Guid prenotazioneId, CancellationToken cancellationToken);

    Task AddAsync(Notifica notifica, CancellationToken cancellationToken);

    Task UpdateAsync(Notifica notifica, CancellationToken cancellationToken);
}
