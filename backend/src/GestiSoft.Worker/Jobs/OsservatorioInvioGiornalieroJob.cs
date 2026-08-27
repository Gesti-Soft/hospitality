using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Osservatorio;
using Quartz;

namespace GestiSoft.Worker.Jobs;

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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invio Osservatorio Turistico: eccezione per struttura {strutturaId}", struttura.StrutturaId);
            }
        }
    }
}
