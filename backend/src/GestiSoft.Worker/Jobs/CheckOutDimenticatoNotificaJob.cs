using GestiSoft.Application.Notifiche;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Enums;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Notifica in-app "check-out dimenticato" (richiesta esplicita dell'utente): prenotazioni In corso
/// (check-in già fatto) il cui check-out previsto è nel passato. Una notifica per prenotazione, non
/// per giorno (CreaPerPrenotazioneSeNonEsisteAsync) — resta visibile finché non viene letta o il
/// check-out viene effettivamente fatto (auto-risolta da PrenotazioniService.CheckOutAsync), non
/// spamma ad ogni giro.
/// </summary>
[DisallowConcurrentExecution]
public class CheckOutDimenticatoNotificaJob(
    IPrenotazioneRepository prenotazioni,
    NotificaService notificaService,
    ILogger<CheckOutDimenticatoNotificaJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var dimenticati = await prenotazioni.ListCheckOutDimenticatoAsync(context.CancellationToken);

        foreach (var prenotazione in dimenticati)
        {
            try
            {
                var numero = prenotazione.NumeroPrenotazione ?? prenotazione.Id.ToString()[..8];
                var giorni = (DateTime.UtcNow.Date - prenotazione.CheckOut!.Value.Date).Days;
                await notificaService.CreaPerPrenotazioneSeNonEsisteAsync(
                    prenotazione.StrutturaId, TipoNotifica.CheckOutDimenticato, prenotazione.Id,
                    "Check-out dimenticato",
                    $"Prenotazione #{numero}: check-out previsto per il {prenotazione.CheckOut:dd/MM/yyyy} ({giorni} giorni fa) non ancora effettuato.",
                    context.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notifica check-out dimenticato: eccezione per prenotazione {id}", prenotazione.Id);
            }
        }
    }
}
