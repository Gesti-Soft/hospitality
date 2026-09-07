using GestiSoft.Application.Auth;
using GestiSoft.Application.Notifiche;
using GestiSoft.Domain.Enums;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Notifica in-app di scadenza della licenza software GestiSoft (Struttura.ScadenzaLicenza — NON la
/// licenza Wubook), su richiesta esplicita dell'utente: un avviso ripetuto ogni giorno (non una
/// volta sola) nei 15 giorni prima della scadenza, e uno ripetuto ogni giorno anche a scadenza
/// avvenuta finché non viene rinnovata — così un operatore che ignora/archivia la notifica di un
/// giorno la ritrova comunque il giorno dopo, invece di doverla andare a cercare. Gira ogni ora su
/// tutte le Strutture attive; la deduplica è per giorno di calendario (ChiaveDedup include la data
/// odierna, non quella di scadenza), quindi al massimo una notifica al giorno per struttura anche se
/// il job gira più volte nello stesso giorno.
/// </summary>
[DisallowConcurrentExecution]
public class LicenzaScadenzaNotificaJob(
    IStrutturaRepository strutture,
    NotificaService notificaService,
    ILogger<LicenzaScadenzaNotificaJob> logger) : IJob
{
    private const int GiorniPreavviso = 15;

    public async Task Execute(IJobExecutionContext context)
    {
        var tutte = await strutture.ListByClienteAsync(null, includiInattive: false, context.CancellationToken);
        var oggi = DateTime.UtcNow.Date;
        var chiaveOggi = oggi.ToString("yyyyMMdd");

        foreach (var struttura in tutte)
        {
            if (struttura.ScadenzaLicenza is not { } scadenza)
            {
                continue;
            }

            var giorniMancanti = (scadenza.Date - oggi).Days;

            try
            {
                if (giorniMancanti < 0)
                {
                    await notificaService.CreaSeNonEsisteAsync(
                        struttura.Id, TipoNotifica.LicenzaScaduta,
                        $"licenza-scaduta:{struttura.Id}:{chiaveOggi}",
                        "Licenza GestiSoft scaduta",
                        $"La licenza GestiSoft di questa struttura è scaduta il {scadenza:dd/MM/yyyy}. Contattare l'assistenza per il rinnovo.",
                        context.CancellationToken);
                }
                else if (giorniMancanti <= GiorniPreavviso)
                {
                    await notificaService.CreaSeNonEsisteAsync(
                        struttura.Id, TipoNotifica.LicenzaInScadenza,
                        $"licenza-scadenza:{struttura.Id}:{chiaveOggi}",
                        "Licenza GestiSoft in scadenza",
                        $"La licenza GestiSoft di questa struttura scade il {scadenza:dd/MM/yyyy} (tra {giorniMancanti} giorni).",
                        context.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notifica scadenza licenza: eccezione per struttura {strutturaId}", struttura.Id);
            }
        }
    }
}
