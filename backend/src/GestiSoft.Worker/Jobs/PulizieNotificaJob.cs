using System.Globalization;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Notifiche;
using GestiSoft.Application.Pulizie;
using GestiSoft.Domain.Enums;
using Quartz;

namespace GestiSoft.Worker.Jobs;

/// <summary>
/// Avviso delle camere da pulire, il giorno prima e il giorno stesso (richiesta dell'utente): lo
/// vede solo chi ha il permesso "Stato camera" (addetti pulizie, receptionist, amministratori).
/// <list type="bullet">
/// <item>"Pulizie di domani" dalle 17: a quell'ora le prenotazioni della giornata sono quasi tutte
/// dentro, ed è quando si organizza il turno del giorno dopo;</item>
/// <item>"Pulizie di oggi" dalle 7, per l'inizio del turno, fino alle 14: più tardi non serve più.</item>
/// </list>
/// Una notifica per struttura e per giorno (chiave di deduplica), nessuna se non c'è niente da fare.
/// </summary>
[DisallowConcurrentExecution]
public class PulizieNotificaJob(
    IStrutturaRepository strutture,
    PulizieSoggiornoService pulizie,
    NotificaService notificaService,
    ILogger<PulizieNotificaJob> logger) : IJob
{
    private const int OraAvvisoOggiDa = 7;
    private const int OraAvvisoOggiA = 14;
    private const int OraAvvisoDomaniDa = 17;

    private static readonly TimeZoneInfo FusoItaliano = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
    private static readonly CultureInfo Italiano = CultureInfo.GetCultureInfo("it-IT");

    public async Task Execute(IJobExecutionContext context)
    {
        var ora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoItaliano);
        var oggi = ora.Date;

        var giorni = new List<(DateTime Giorno, string Quando)>();
        if (ora.Hour >= OraAvvisoOggiDa && ora.Hour < OraAvvisoOggiA)
        {
            giorni.Add((oggi, "oggi"));
        }

        if (ora.Hour >= OraAvvisoDomaniDa)
        {
            giorni.Add((oggi.AddDays(1), "domani"));
        }

        if (giorni.Count == 0)
        {
            return;
        }

        var tutte = await strutture.ListByClienteAsync(null, includiInattive: false, context.CancellationToken);
        foreach (var struttura in tutte)
        {
            foreach (var (giorno, quando) in giorni)
            {
                try
                {
                    var riepilogo = await pulizie.RiepilogoAsync(struttura.Id, giorno, context.CancellationToken);
                    if (riepilogo.Vuoto)
                    {
                        continue;
                    }

                    await notificaService.CreaSeNonEsisteAsync(
                        struttura.Id,
                        TipoNotifica.PulizieDaFare,
                        $"pulizie:{quando}:{struttura.Id}:{giorno:yyyyMMdd}",
                        $"Pulizie di {quando}",
                        $"{Maiuscola(giorno.ToString("dddd d MMMM", Italiano))}: {Descrivi(riepilogo)}.",
                        context.CancellationToken,
                        richiedeStatoCamera: true);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Notifica pulizie: eccezione per la struttura {id}", struttura.Id);
                }
            }
        }
    }

    private static string Maiuscola(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Italiano) + s[1..];

    private static string Descrivi(PulizieSoggiornoService.RiepilogoGiorno r)
    {
        var parti = new List<string>();
        if (r.Partenze > 0) parti.Add(r.Partenze == 1 ? "1 partenza da rifare" : $"{r.Partenze} partenze da rifare");
        if (r.Pulizie > 0) parti.Add(r.Pulizie == 1 ? "1 pulizia in camera occupata" : $"{r.Pulizie} pulizie in camere occupate");
        if (r.CambiBiancheria > 0) parti.Add(r.CambiBiancheria == 1 ? "1 cambio biancheria" : $"{r.CambiBiancheria} cambi biancheria");
        return string.Join(", ", parti);
    }
}
