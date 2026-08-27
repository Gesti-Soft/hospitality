using GestiSoft.Application.Wubook;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>Polling minute-by-minute degli eventi Wubook via gestisoft.it — porta il minute timer di OtaService.exe del legacy (ComunicationLogic.GetPrenotations).</summary>
[DisallowConcurrentExecution]
public class WubookEventiPollingJob(
    IWubookIntegrazioneRepository integrazioni,
    WubookEventiService eventiService,
    ILogger<WubookEventiPollingJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var attive = await integrazioni.ListAttiveAsync(context.CancellationToken);

        foreach (var integrazione in attive)
        {
            try
            {
                await eventiService.ElaboraEventiAsync(integrazione.StrutturaId, context.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Polling eventi Wubook: eccezione per struttura {strutturaId}", integrazione.StrutturaId);
            }
        }
    }
}
