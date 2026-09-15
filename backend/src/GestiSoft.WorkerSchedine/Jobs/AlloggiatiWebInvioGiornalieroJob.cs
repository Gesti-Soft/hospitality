using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Notifiche;
using GestiSoft.Domain.Enums;
using Quartz;

namespace GestiSoft.WorkerSchedine.Jobs;

/// <summary>
/// Invio giornaliero schedine Alloggiati Web — porta lo StartDailyTaskTimer/CheckTimeAndExecuteTask
/// di SyncStatePolice.exe del legacy (un timer che controllava ogni secondo se l'ora corrente
/// coincideva con quella configurata). Qui gira ogni minuto per tutte le Strutture con
/// ImpostazioniStruttura.PoliziaStatoAttiva attivo, confrontando l'ora locale con
/// OraInvioGiornaliero (">=" invece di uguaglianza esatta, per non perdere la finestra se un giro
/// del job viene saltato). Il batch non si ripete una volta riuscito, e in caso di errore si
/// ritenta un numero limitato di volte con attesa crescente invece che ad ogni giro: vedi
/// <see cref="Domain.Entities.PoliticaTentativi"/>, condivisa con Osservatorio e PayTourist.
/// </summary>
[DisallowConcurrentExecution]
public class AlloggiatiWebInvioGiornalieroJob(
    IImpostazioniStrutturaRepository impostazioni,
    IAlloggiatiWebIntegrazioneRepository integrazioni,
    AlloggiatiWebInvioService invioService,
    NotificaService notificaService,
    ILogger<AlloggiatiWebInvioGiornalieroJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var strutture = await impostazioni.ListAttivePerPoliziaAsync(context.CancellationToken);
        var oraCorrente = TimeOnly.FromDateTime(DateTime.Now);
        var adesso = DateTime.UtcNow;
        var oggi = adesso.Date;

        foreach (var struttura in strutture)
        {
            if (struttura.OraInvioGiornaliero is not { } orarioConfigurato || oraCorrente < orarioConfigurato)
            {
                continue;
            }

            var integrazione = await integrazioni.GetByStrutturaIdAsync(struttura.StrutturaId, context.CancellationToken);
            if (integrazione is not null && !DaProcessare(integrazione, adesso, oggi))
            {
                continue;
            }

            try
            {
                var risultato = await invioService.InviaSistemaAsync(struttura.StrutturaId, context.CancellationToken);
                logger.LogInformation(
                    "Invio Alloggiati Web struttura {strutturaId}: {inviate}/{totale} inviate ({errori} errori){messaggio}",
                    struttura.StrutturaId, risultato.Inviate, risultato.TotaleSchedine, risultato.Errori,
                    risultato.Messaggio is null ? string.Empty : $" - {risultato.Messaggio}");

                if (risultato.TotaleSchedine > 0)
                {
                    await notificaService.CreaSeNonEsisteAsync(
                        struttura.StrutturaId, TipoNotifica.SchedineInviate,
                        $"schedine:alloggiati-web:{struttura.StrutturaId}:{oggi:yyyyMMdd}",
                        "Alloggiati Web: schedine inviate",
                        $"Alloggiati Web: {risultato.Inviate}/{risultato.TotaleSchedine} schedine inviate oggi" + (risultato.Errori > 0 ? $" ({risultato.Errori} errori)." : "."),
                        context.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invio Alloggiati Web: eccezione per struttura {strutturaId}", struttura.StrutturaId);
            }
        }
    }

    /// <summary>
    /// Se questo giro deve occuparsi della struttura. Si salta quando l'invio di oggi è già andato
    /// a buon fine, e quando i tentativi della giornata sono esauriti o l'attesa dopo l'ultimo
    /// fallimento non è ancora trascorsa. Il controllo su <c>UltimoInvioAtUtc</c> senza errore copre
    /// le integrazioni che hanno già inviato **prima** che i contatori esistessero: senza, il giorno
    /// del rilascio il batch ripartirebbe una volta a vuoto.
    /// </summary>
    private static bool DaProcessare(Domain.Entities.AlloggiatiWebIntegrazione integrazione, DateTime adesso, DateTime oggi)
    {
        if (Domain.Entities.PoliticaTentativi.GiaRiuscitoOggi(integrazione, adesso))
        {
            return false;
        }

        if (integrazione.UltimoErrore is null && integrazione.UltimoInvioAtUtc?.Date == oggi)
        {
            return false;
        }

        return Domain.Entities.PoliticaTentativi.PuoTentare(integrazione, adesso);
    }
}
