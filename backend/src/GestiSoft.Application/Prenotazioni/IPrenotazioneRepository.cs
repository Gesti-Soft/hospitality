using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Prenotazioni;

public interface IPrenotazioneRepository
{
    Task<Prenotazione?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Prenotazioni future non ancora iniziate (GetOspitisByRoom del legacy).</summary>
    Task<IReadOnlyList<Prenotazione>> ListInArrivoAsync(Guid strutturaId, DateTime daData, CancellationToken cancellationToken);

    /// <summary>Ospiti attualmente in camera, check-in già effettuato (GetAlloggiatiByRoom del legacy).</summary>
    Task<IReadOnlyList<Prenotazione>> ListInCorsoAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Storico (Annullata/Completata) filtrato per anno (GetStoricoPrenotazioniByRoom del legacy).</summary>
    Task<IReadOnlyList<Prenotazione>> ListStoricoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>
    /// True se esiste già una prenotazione non annullata sulla stessa camera che si sovrappone
    /// al periodo indicato (esclusa <paramref name="escludiPrenotazioneId"/> in caso di update).
    /// </summary>
    Task<bool> EsisteSovrapposizioneAsync(
        Guid strutturaId,
        Guid cameraId,
        DateTime checkIn,
        DateTime checkOut,
        Guid? escludiPrenotazioneId,
        CancellationToken cancellationToken);

    /// <summary>Conteggio prenotazioni dirette (Agenzia == "Diretta") dell'anno, per il numero prenotazione suggerito.</summary>
    Task<int> ContaDireteAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    Task AddAsync(Prenotazione entity, CancellationToken cancellationToken);

    Task UpdateAsync(Prenotazione entity, CancellationToken cancellationToken);
}
