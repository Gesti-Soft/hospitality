using GestiSoft.Application.Wubook;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Rinnovo periodico delle credenziali Wubook (tokenWb/idWoBook) per ogni Struttura con
/// l'integrazione attiva — porta GetWobook/ValidateUserCredentials del legacy, chiamato
/// all'avvio del processo OtaService.exe e poi ogni 2 ore. Qui gira per TUTTE le strutture attive
/// (multi-tenant), non per un'unica installazione.
/// </summary>
[DisallowConcurrentExecution]
public class WubookLicenzaRefreshJob(
    IWubookIntegrazioneRepository integrazioni,
    WubookLicenzaService licenzaService,
    ILogger<WubookLicenzaRefreshJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var attive = await integrazioni.ListAttiveAsync(context.CancellationToken);
        logger.LogInformation("Rinnovo credenziali Wubook: {count} strutture attive", attive.Count);

        foreach (var integrazioneNonTracciata in attive)
        {
            try
            {
                // Bug trovato testando live durante la Fase 6: ListAttiveAsync legge con
                // AsNoTracking, quindi passare quell'istanza direttamente a RinnovaCredenzialiAsync
                // (che internamente fa un Upsert) la faceva risultare "Detached" anche se la riga
                // esisteva già in DB, causando un INSERT con PK duplicata invece di un UPDATE — si
                // ripresentava ad ogni ciclo di rinnovo (ogni 2h) per qualunque struttura già
                // configurata. Corretto ri-agganciando l'entità tracciata prima del rinnovo.
                var integrazione = await integrazioni.GetByStrutturaIdAsync(integrazioneNonTracciata.StrutturaId, context.CancellationToken)
                    ?? integrazioneNonTracciata;

                var aggiornata = await licenzaService.RinnovaCredenzialiAsync(integrazione, context.CancellationToken);
                if (aggiornata.UltimoErrore is { } errore)
                {
                    logger.LogWarning("Rinnovo credenziali Wubook fallito per struttura {strutturaId}: {errore}", integrazione.StrutturaId, errore);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Rinnovo credenziali Wubook: eccezione per struttura {strutturaId}", integrazioneNonTracciata.StrutturaId);
            }
        }
    }
}
