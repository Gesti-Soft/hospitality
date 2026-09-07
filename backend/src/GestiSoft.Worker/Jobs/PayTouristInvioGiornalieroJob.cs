using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Notifiche;
using GestiSoft.Application.PayTourist;
using GestiSoft.Domain.Enums;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Invio giornaliero PayTourist — stessa cadenza/orario condiviso di Alloggiati Web/Osservatorio
/// (il legacy usava un unico "ORA INVIO SCHEDINE" per tutti e tre i servizi). Gira ogni minuto per
/// tutte le Strutture con <c>ImpostazioniStruttura.PayTouristAttivo</c>, confrontando l'ora locale
/// con <c>OraInvioGiornaliero</c> (">=" invece di uguaglianza esatta, stesso motivo di Alloggiati
/// Web). A differenza di quel job — che ha una sola integrazione per Struttura — una Struttura può
/// avere più "strutture" PayTourist configurate (vedi <see cref="Domain.Entities.PayTouristStruttura"/>):
/// il giro giornaliero viene saltato solo se TUTTE risultano già inviate oggi.
/// </summary>
[DisallowConcurrentExecution]
public class PayTouristInvioGiornalieroJob(
    IImpostazioniStrutturaRepository impostazioni,
    IPayTouristStrutturaRepository payTouristStrutture,
    PayTouristInvioService invioService,
    NotificaService notificaService,
    ILogger<PayTouristInvioGiornalieroJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var strutture = await impostazioni.ListAttivePerPayTouristAsync(context.CancellationToken);
        var oraCorrente = TimeOnly.FromDateTime(DateTime.Now);
        var oggi = DateTime.UtcNow.Date;

        foreach (var struttura in strutture)
        {
            if (struttura.OraInvioGiornaliero is not { } orarioConfigurato || oraCorrente < orarioConfigurato)
            {
                continue;
            }

            var strutturePayTourist = await payTouristStrutture.ListByStrutturaAsync(struttura.StrutturaId, context.CancellationToken);
            if (strutturePayTourist.Count > 0 && strutturePayTourist.All(s => s.UltimoInvioAtUtc?.Date == oggi))
            {
                continue;
            }

            try
            {
                var risultato = await invioService.InviaSistemaAsync(struttura.StrutturaId, context.CancellationToken);
                logger.LogInformation(
                    "Invio PayTourist struttura {strutturaId}: {inviate}/{totale} inviate ({errori} errori){messaggio}",
                    struttura.StrutturaId, risultato.Inviate, risultato.TotalePrenotazioni, risultato.Errori,
                    risultato.Messaggio is null ? string.Empty : $" - {risultato.Messaggio}");

                if (risultato.TotalePrenotazioni > 0)
                {
                    await notificaService.CreaSeNonEsisteAsync(
                        struttura.StrutturaId, TipoNotifica.SchedineInviate,
                        $"schedine:paytourist:{struttura.StrutturaId}:{oggi:yyyyMMdd}",
                        "PayTourist: schedine inviate",
                        $"PayTourist: {risultato.Inviate}/{risultato.TotalePrenotazioni} schedine inviate oggi" + (risultato.Errori > 0 ? $" ({risultato.Errori} errori)." : "."),
                        context.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invio PayTourist: eccezione per struttura {strutturaId}", struttura.StrutturaId);
            }
        }
    }
}
