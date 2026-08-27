using GestiSoft.Application.Wubook;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>Pull orario delle nuove prenotazioni Wubook (fetch_new_bookings) — porta l'hour timer di OtaService.exe del legacy, per tutte le strutture con integrazione attiva.</summary>
[DisallowConcurrentExecution]
public class WubookPullPrenotazioniJob(
    IWubookIntegrazioneRepository integrazioni,
    WubookPrenotazioniService prenotazioniService,
    ILogger<WubookPullPrenotazioniJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var attive = await integrazioni.ListAttiveAsync(context.CancellationToken);
        logger.LogInformation("Pull prenotazioni Wubook: {count} strutture attive", attive.Count);

        foreach (var integrazione in attive)
        {
            try
            {
                var risultato = await prenotazioniService.SincronizzaSistemaAsync(integrazione.StrutturaId, context.CancellationToken);
                logger.LogInformation(
                    "Pull prenotazioni Wubook struttura {strutturaId}: {importate} importate, {aggiornate} aggiornate, {annullate} annullate, {errori} errori",
                    integrazione.StrutturaId, risultato.Importate, risultato.Aggiornate, risultato.Annullate, risultato.Errori);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pull prenotazioni Wubook: eccezione per struttura {strutturaId}", integrazione.StrutturaId);
            }
        }
    }
}
