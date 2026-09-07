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

    /// <summary>Tutte le prenotazioni non annullate che intersecano il periodo, su tutte le camere della struttura — usata dal booking board del frontend (Fase 9).</summary>
    Task<IReadOnlyList<Prenotazione>> ListPeriodoAsync(Guid strutturaId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken);

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

    /// <summary>Somma ImportoPagato delle prenotazioni non annullate dell'anno — usata dal riepilogo cassa di Fase 4.</summary>
    Task<decimal> SommaImportoPagatoAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>Somma ImportoPagato di tutte le prenotazioni non annullate, senza filtro anno — per la cassa cumulativa (quanto dovrebbe esserci ad oggi, da sempre).</summary>
    Task<decimal> SommaImportoPagatoTotaleAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Anni con almeno un incasso (prenotazione non annullata con ImportoPagato &gt; 0) — per il selettore Anno del riepilogo cassa.</summary>
    Task<IReadOnlyList<int>> ListaAnniConIncassoAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>
    /// Anni con almeno una prenotazione non annullata — per il selettore Anno delle schermate
    /// operative Alloggiati Web/Osservatorio/PayTourist (su richiesta esplicita, non deve proporre
    /// anni sicuramente vuoti, stesso principio già applicato a Statistiche/Finanze).
    /// </summary>
    Task<IReadOnlyList<int>> ListaAnniConPrenotazioniAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Prenotazioni non annullate di una camera che si sovrappongono al periodo — usata dalla sincronizzazione disponibilità Wubook di Fase 5.</summary>
    Task<IReadOnlyList<Prenotazione>> ListOccupazioneAsync(Guid strutturaId, Guid cameraId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken);

    Task<Prenotazione?> GetByIdPrenotazioneWubookAsync(Guid strutturaId, int idPrenotazioneWubook, CancellationToken cancellationToken);

    /// <summary>Prenotazioni In corso (check-in già fatto) il cui check-out previsto è nel passato, su tutte le Strutture — usata dal job di notifica "check-out dimenticato".</summary>
    Task<IReadOnlyList<Prenotazione>> ListCheckOutDimenticatoAsync(CancellationToken cancellationToken);

    Task AddAsync(Prenotazione entity, CancellationToken cancellationToken);

    Task UpdateAsync(Prenotazione entity, CancellationToken cancellationToken);
}
