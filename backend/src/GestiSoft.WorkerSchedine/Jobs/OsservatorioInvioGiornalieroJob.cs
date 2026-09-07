using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Notifiche;
using GestiSoft.Application.Osservatorio;
using GestiSoft.Domain.Enums;
using Quartz;

namespace GestiSoft.WorkerSchedine.Jobs;

/// <summary>
/// Invio giornaliero Osservatorio Turistico — stesso orario configurato di Alloggiati Web
/// (<c>ImpostazioniStruttura.OraInvioGiornaliero</c>, condiviso tra le integrazioni "schedine" fin
/// dalla Fase 2), gate diverso però: qui non serve un flag "già inviato oggi" separato, perché il
/// cursore per-appartamento (<see cref="Domain.Entities.OsservatorioAppartamento.CursoreDataAtUtc"/>)
/// avanza solo a chiusura giornata riuscita — se il cursore è già a domani, il prossimo giro di
/// oggi trova <c>cursore &gt; oggi</c> e non fa nulla.
/// </summary>
[DisallowConcurrentExecution]
public class OsservatorioInvioGiornalieroJob(
    IImpostazioniStrutturaRepository impostazioni,
    OsservatorioInvioService invioService,
    NotificaService notificaService,
    ILogger<OsservatorioInvioGiornalieroJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var strutture = await impostazioni.ListAttivePerOsservatorioAsync(context.CancellationToken);
        var oraCorrente = TimeOnly.FromDateTime(DateTime.Now);

        foreach (var struttura in strutture)
        {
            if (struttura.OraInvioGiornaliero is not { } orarioConfigurato || oraCorrente < orarioConfigurato)
            {
                continue;
            }

            try
            {
                var risultati = await invioService.InviaSistemaAsync(struttura.StrutturaId, context.CancellationToken);
                foreach (var risultato in risultati)
                {
                    logger.LogInformation(
                        "Invio Osservatorio Turistico struttura {strutturaId}: {arrivi} arrivi, {checkout} checkout, {giorni} giorni chiusi{messaggio}",
                        struttura.StrutturaId, risultato.ArriviInviati, risultato.CheckoutInviati, risultato.GiorniChiusi,
                        risultato.Messaggio is null ? string.Empty : $" - {risultato.Messaggio}");
                }

                // Una sola notifica riepilogativa al giorno per struttura (somma su tutti gli
                // appartamenti configurati) — a differenza di Alloggiati Web/PayTourist, questo job
                // non si auto-limita a un'esecuzione al giorno (ritenta ogni minuto finché il cursore
                // non avanza), quindi qui la deduplica per data è l'unica cosa che evita una notifica
                // ad ogni giro riuscito.
                var arriviTotali = risultati.Sum(r => r.ArriviInviati);
                var checkoutTotali = risultati.Sum(r => r.CheckoutInviati);
                if (arriviTotali > 0 || checkoutTotali > 0)
                {
                    await notificaService.CreaSeNonEsisteAsync(
                        struttura.StrutturaId, TipoNotifica.SchedineInviate,
                        $"schedine:osservatorio:{struttura.StrutturaId}:{DateTime.UtcNow:yyyyMMdd}",
                        "Osservatorio Turistico: schedine inviate",
                        $"Osservatorio Turistico: {arriviTotali} arrivi e {checkoutTotali} check-out inviati oggi.",
                        context.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invio Osservatorio Turistico: eccezione per struttura {strutturaId}", struttura.StrutturaId);
            }
        }
    }
}
