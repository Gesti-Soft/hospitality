using GestiSoft.Application.Notifiche;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

/// <summary>
/// Polling minute-by-minute — porta ComunicationLogic.GetPrenotations del legacy: interroga
/// gestisoft.it per gli eventi Wubook "non letti" (notificati a gestisoft.it dal webhook push di
/// Wubook, non da un timestamp incrementale nostro), recupera ciascuna prenotazione con
/// fetch_booking e la marca come letta su gestisoft.it a elaborazione riuscita. Riusa
/// WubookPrenotazioniService.ImportaBookingRicevutoAsync per non duplicare la logica di mapping.
/// Ogni Lcode/Rcode intercettato viene anche registrato in locale (WubookEventoRicevuto,
/// indipendentemente dall'esito): lo storico "letto/non letto" di gestisoft.it serve solo a sapere
/// cosa manca da elaborare, non è pensato per recuperare a posteriori una prenotazione — questa
/// copia locale sì.
/// Chiamato solo dal job Quartz (Worker) — nessun controllo permessi utente, gira per il sistema.
/// </summary>
public class WubookEventiService(
    IWubookIntegrazioneRepository integrazioni,
    IGestisoftLicenzaClient licenzaClient,
    IWubookClient wubookClient,
    IWubookEventoRicevutoRepository eventiRicevuti,
    WubookLicenzaService licenzaService,
    WubookPrenotazioniService prenotazioniService,
    NotificaService notificaService)
{
    public async Task ElaboraEventiAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (integrazione is not { Attivo: true } || string.IsNullOrWhiteSpace(integrazione.GestisoftToken))
        {
            return;
        }

        // Eseguito ad ogni giro (non solo quando ci sono eventi nuovi da gestisoft.it, vedi ritorno
        // anticipato sotto): promuove a "cancellata" visibile le cancellazioni la cui finestra di
        // grazia (vedi NotificaService.RegistraCancellazioneWubookAsync) è scaduta senza che sia
        // arrivata una prenotazione corrispondente.
        await notificaService.ConfermaCancellazioniScaduteAsync(strutturaId, cancellationToken);

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
                await RegistraEventoAsync(strutturaId, lcode, rcode, riuscita: false, errore: "Impossibile recuperare la prenotazione da Wubook (fetch_booking).", cancellationToken);
                continue;
            }

            try
            {
                var nomeCanale = canali.FirstOrDefault(c => c.Id == booking.IdChannel)?.Nome ?? "Sito Web";
                await prenotazioniService.ImportaBookingRicevutoAsync(strutturaId, booking, nomeCanale, cancellationToken);
                await RegistraEventoAsync(strutturaId, lcode, rcode, riuscita: true, errore: null, cancellationToken);
                rcodesElaborati.Add(rcodeRaw);
            }
            catch (Exception ex)
            {
                // Non marcato come letto su gestisoft.it: verrà ritentato al prossimo giro (il job
                // Worker logga comunque l'eccezione a livello aggregato) — qui registriamo solo
                // l'ultimo motivo del fallimento per chi dovrà recuperarlo a mano.
                await RegistraEventoAsync(strutturaId, lcode, rcode, riuscita: false, errore: ex.Message, cancellationToken);
            }
        }

        if (rcodesElaborati.Count > 0)
        {
            await licenzaClient.MarkReadAsync(integrazione.GestisoftToken, rcodesElaborati, cancellationToken);
        }
    }

    private async Task RegistraEventoAsync(Guid strutturaId, string lcode, int rcode, bool riuscita, string? errore, CancellationToken cancellationToken)
    {
        var evento = await eventiRicevuti.GetByRcodeAsync(strutturaId, rcode, cancellationToken) ?? new WubookEventoRicevuto { StrutturaId = strutturaId, Rcode = rcode };

        evento.Lcode = lcode;
        evento.ImportazioneRiuscita = riuscita;
        evento.MessaggioErrore = errore;
        evento.UpdatedAtUtc = DateTime.UtcNow;

        await eventiRicevuti.UpsertAsync(evento, cancellationToken);
    }
}
