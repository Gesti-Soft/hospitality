using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Prenotazioni;

/// <summary>
/// Risolve "la prima camera libera di una Tipologia (pool)" per un intervallo di date — usata sia
/// da <see cref="PrenotazioniService"/> (prenotazione creata scegliendo solo la Tipologia, non una
/// camera specifica) sia da GestiSoft.Application.Wubook.WubookPrenotazioniService (booking OTA in
/// arrivo su un pool associato). Estratta come servizio dedicato (invece che iniettare un servizio
/// nell'altro) per evitare una dipendenza incrociata tra i due moduli applicativi.
/// </summary>
public class AssegnazioneCameraService(ICameraRepository camere, IPrenotazioneRepository prenotazioni)
{
    /// <summary>Ritorna la prima camera del pool senza sovrapposizioni per il periodo, o null se sono tutte occupate.</summary>
    public async Task<SettingRoom?> TrovaCameraLiberaAsync(
        Guid strutturaId,
        Guid tipologiaId,
        DateTime checkIn,
        DateTime checkOut,
        Guid? escludiPrenotazioneId,
        CancellationToken cancellationToken)
    {
        var candidate = await camere.ListByTipologiaAsync(strutturaId, tipologiaId, cancellationToken);
        foreach (var candidata in candidate)
        {
            var sovrapposta = await prenotazioni.EsisteSovrapposizioneAsync(strutturaId, candidata.Id, checkIn, checkOut, escludiPrenotazioneId, cancellationToken);
            if (!sovrapposta)
            {
                return candidata;
            }
        }

        return null;
    }

    /// <summary>Come <see cref="TrovaCameraLiberaAsync"/>, ma lancia se nessuna camera è libera — per il percorso "manuale" (l'operatore deve saperlo subito, non una prenotazione lasciata senza camera).</summary>
    public async Task<SettingRoom> RisolviCameraLiberaAsync(
        Guid strutturaId,
        Guid tipologiaId,
        DateTime checkIn,
        DateTime checkOut,
        Guid? escludiPrenotazioneId,
        CancellationToken cancellationToken)
    {
        return await TrovaCameraLiberaAsync(strutturaId, tipologiaId, checkIn, checkOut, escludiPrenotazioneId, cancellationToken)
            ?? throw new ConflictException("Nessuna camera libera di questa tipologia per le date scelte.");
    }
}
