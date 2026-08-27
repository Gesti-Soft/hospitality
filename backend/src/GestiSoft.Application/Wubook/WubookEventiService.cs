using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

/// <summary>
/// Polling minute-by-minute — porta ComunicationLogic.GetPrenotations del legacy: interroga
/// gestisoft.it per gli eventi Wubook "non letti" (notificati a gestisoft.it dal webhook push di
/// Wubook, non da un timestamp incrementale nostro), recupera ciascuna prenotazione con
/// fetch_booking e la marca come letta su gestisoft.it a elaborazione riuscita. Riusa
/// WubookPrenotazioniService.ImportaBookingRicevutoAsync per non duplicare la logica di mapping.
/// Chiamato solo dal job Quartz (Worker) — nessun controllo permessi utente, gira per il sistema.
/// </summary>
public class WubookEventiService(
    IWubookIntegrazioneRepository integrazioni,
    IGestisoftLicenzaClient licenzaClient,
    IWubookClient wubookClient,
    WubookLicenzaService licenzaService,
    WubookPrenotazioniService prenotazioniService)
{
    public async Task ElaboraEventiAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (integrazione is not { Attivo: true } || string.IsNullOrWhiteSpace(integrazione.GestisoftToken))
        {
            return;
        }

        var eventi = await licenzaClient.GetEventiNonLettiAsync(integrazione.GestisoftToken, cancellationToken);
        if (eventi.Status != "ok" || eventi.RCodes.Count == 0)
        {
            return;
        }

        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        var canali = await wubookClient.GetChannelsInfoAsync(token, cancellationToken);
        var rcodesElaborati = new List<string>();

        foreach (var rcodeRaw in eventi.RCodes)
        {
            if (!int.TryParse(rcodeRaw, out var rcode))
            {
                continue;
            }

            var booking = await wubookClient.FetchBookingAsync(token, lcode, rcode, cancellationToken);
            if (booking is null)
            {
                continue;
            }

            try
            {
                var nomeCanale = canali.FirstOrDefault(c => c.Id == booking.IdChannel)?.Nome ?? "Sito Web";
                await prenotazioniService.ImportaBookingRicevutoAsync(strutturaId, booking, nomeCanale, cancellationToken);
                rcodesElaborati.Add(rcodeRaw);
            }
            catch
            {
                // Non marcato come letto: verrà ritentato al prossimo giro (nessun log qui, il job Worker logga l'eccezione a livello aggregato).
            }
        }

        if (rcodesElaborati.Count > 0)
        {
            await licenzaClient.MarkReadAsync(integrazione.GestisoftToken, rcodesElaborati, cancellationToken);
        }
    }
}
