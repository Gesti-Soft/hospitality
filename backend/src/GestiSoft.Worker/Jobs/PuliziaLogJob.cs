using GestiSoft.Application.Logging;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Applica la politica di conservazione di LogEvento (richiesta esplicita dell'utente, principio di
/// "limitazione della conservazione" GDPR art. 5.1.e): elimina gli Info più vecchi di
/// LogEventoService.GiorniConservazioneInfo e Warning/Error più vecchi di GiorniConservazioneAltri.
/// Cancellazione netta, non anonimizzazione — scelta esplicita dell'utente. Un giro al giorno
/// basta: non è un'operazione urgente, un ritardo di qualche ora non cambia nulla.
/// </summary>
[DisallowConcurrentExecution]
public class PuliziaLogJob(ILogEventoService logEventoService, ILogger<PuliziaLogJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var eliminati = await logEventoService.PulisciVecchiAsync(context.CancellationToken);
        if (eliminati > 0)
        {
            logger.LogInformation("Pulizia log: eliminati {n} eventi oltre la soglia di conservazione", eliminati);
        }
    }
}
