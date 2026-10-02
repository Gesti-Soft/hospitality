using GestiSoft.Application.Notifiche;
using GestiSoft.Application.Wubook;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Polling minute-by-minute degli eventi Wubook via gestisoft.it — porta il minute timer di OtaService.exe del legacy (ComunicationLogic.GetPrenotations).
/// Per le strutture con ricezione diretta (WubookIntegrazione.AvvisiDiretti) gestisoft.it non si
/// interroga più: si elaborano gli avvisi arrivati all'Api e il controllo periodico (vedi
/// WubookAvvisiDirettiService). Gli avvisi rimasti in coda si elaborano comunque, anche dopo una
/// disattivazione.
/// </summary>
[DisallowConcurrentExecution]
public class WubookEventiPollingJob(
    IWubookIntegrazioneRepository integrazioni,
    WubookEventiService eventiService,
    WubookAvvisiDirettiService avvisiDiretti,
    NotificaService notificaService,
    ILogger<WubookEventiPollingJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var attive = await integrazioni.ListAttiveAsync(context.CancellationToken);

        foreach (var integrazione in attive)
        {
            try
            {
                await avvisiDiretti.ElaboraAsync(integrazione, context.CancellationToken);

                if (integrazione.AvvisiDiretti)
                {
                    // Nel percorso via gestisoft.it lo fa ElaboraEventiAsync a ogni giro.
                    await notificaService.ConfermaCancellazioniScaduteAsync(integrazione.StrutturaId, context.CancellationToken);
                }
                else
                {
                    await eventiService.ElaboraEventiAsync(integrazione.StrutturaId, context.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Polling eventi Wubook: eccezione per struttura {strutturaId}", integrazione.StrutturaId);
            }
        }
    }
}
