using GestiSoft.Application.SuperAdmin;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Elimina le strutture demo create da più di 30 giorni (scelta dell'utente), vedi
/// SuperAdminService.EliminaDemoScaduteAsync. Prima di allora il Super Admin può eliminarle a mano.
/// </summary>
[DisallowConcurrentExecution]
public class EliminazioneDemoJob(SuperAdminService superAdmin, ILogger<EliminazioneDemoJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var eliminate = await superAdmin.EliminaDemoScaduteAsync(context.CancellationToken);
        if (eliminate > 0)
        {
            logger.LogInformation("Demo: eliminate {n} strutture demo create da oltre {giorni} giorni", eliminate, SuperAdminService.GiorniDemo);
        }
    }
}
