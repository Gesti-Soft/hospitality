using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Notifiche;
using GestiSoft.Domain.Enums;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Invio giornaliero schedine Alloggiati Web — porta lo StartDailyTaskTimer/CheckTimeAndExecuteTask
/// di SyncStatePolice.exe del legacy (un timer che controllava ogni secondo se l'ora corrente
/// coincideva con quella configurata). Qui gira ogni minuto per tutte le Strutture con
/// ImpostazioniStruttura.PoliziaStatoAttiva attivo, confrontando l'ora locale con
/// OraInvioGiornaliero (">=" invece di uguaglianza esatta, per non perdere la finestra se un giro
/// del job viene saltato) e usando AlloggiatiWebIntegrazione.UltimoInvioAtUtc — persistito su DB
/// invece che in memoria come il legacy — per non rieseguire il batch più volte nello stesso giorno.
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
        var oggi = DateTime.UtcNow.Date;

        foreach (var struttura in strutture)
        {
            if (struttura.OraInvioGiornaliero is not { } orarioConfigurato || oraCorrente < orarioConfigurato)
            {
                continue;
            }

            var integrazione = await integrazioni.GetByStrutturaIdAsync(struttura.StrutturaId, context.CancellationToken);
            if (integrazione?.UltimoInvioAtUtc?.Date == oggi)
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
}
