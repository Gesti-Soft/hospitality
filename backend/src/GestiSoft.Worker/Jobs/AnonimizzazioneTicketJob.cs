using GestiSoft.Application.Assistenza;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Conservazione dei ticket di assistenza (GDPR art. 5.1.e, scelta dell'utente): anonimizza i ticket
/// chiusi da più di 12 mesi, vedi AssistenzaService.AnonimizzaScadutiAsync. Un giro al giorno basta.
/// </summary>
[DisallowConcurrentExecution]
public class AnonimizzazioneTicketJob(AssistenzaService assistenza, ILogger<AnonimizzazioneTicketJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var anonimizzati = await assistenza.AnonimizzaScadutiAsync(context.CancellationToken);
        if (anonimizzati > 0)
        {
            logger.LogInformation("Assistenza: anonimizzati {n} ticket chiusi da oltre {mesi} mesi", anonimizzati, AssistenzaService.MesiConservazione);
        }
    }
}
