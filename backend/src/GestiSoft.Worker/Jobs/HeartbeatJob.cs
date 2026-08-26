using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Job dimostrativo che valida il cablaggio di Quartz.NET nel Worker. Verrà sostituito dai job
/// reali (invio schedine Alloggiati Web, Osservatorio Turistico, PayTourist, polling Wubook)
/// nelle Fasi 5-8 del piano. Ogni job reale userà `IJobExecutionContext` per limitare la
/// concorrenza per tipo di integrazione (es. tramite `[DisallowConcurrentExecution]` o job group
/// dedicati), così tanti clienti non finiscono per saturare gli endpoint esterni in parallelo.
/// </summary>
[DisallowConcurrentExecution]
public class HeartbeatJob(ILogger<HeartbeatJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Worker heartbeat: {time}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
