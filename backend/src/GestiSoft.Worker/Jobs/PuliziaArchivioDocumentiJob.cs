using GestiSoft.Application.Fatturazione;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Cancella dall'archivio i documenti fiscali delle strutture eliminate quando passano i 10 anni
/// (31 dicembre del decimo anno dalla data del documento): contengono dati personali, oltre
/// l'obbligo di conservazione non c'è motivo di tenerli. Un giro al giorno basta.
/// </summary>
[DisallowConcurrentExecution]
public class PuliziaArchivioDocumentiJob(ArchivioDocumentiService archivio, ILogger<PuliziaArchivioDocumentiJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var eliminati = await archivio.EliminaScadutiAsync(context.CancellationToken);
        if (eliminati > 0)
        {
            logger.LogInformation("Archivio documenti: eliminati {n} documenti scaduti", eliminati);
        }
    }
}
