using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Pulizie;

public record EsitoServizioSoggiorno(StatoServizioSoggiorno Stato, int? IntervalloGiorni, DateTime? Previsto);

/// <summary>
/// Quando tocca la pulizia (o il cambio biancheria) di una camera occupata. Tutto su date civili,
/// senza orari:
/// <list type="bullet">
/// <item>il giorno dell'arrivo non conta, la camera è appena stata preparata;</item>
/// <item>la prossima cade N giorni dopo l'ultima fatta, o dopo l'arrivo se non ce n'è ancora una;</item>
/// <item>il giorno della partenza non è mai un servizio intermedio: c'è già la pulizia del check-out;</item>
/// <item>una prevista e non fatta resta "in ritardo" finché non si segna o finché l'ospite parte.</item>
/// </list>
/// </summary>
public static class CalcoloServizioSoggiorno
{
    /// <summary>
    /// Il più specifico vince: la tipologia se ha un valore (0 = nessuna), altrimenti la struttura.
    /// La rinuncia dell'ospite si applica sopra, in <see cref="Calcola"/>.
    /// </summary>
    public static int? IntervalloEffettivo(int? intervalloTipologia, int? intervalloStruttura)
    {
        var intervallo = intervalloTipologia ?? intervalloStruttura;
        return intervallo is > 0 ? intervallo : null;
    }

    public static EsitoServizioSoggiorno Calcola(
        DateTime? arrivo,
        DateTime? partenza,
        DateTime? ultimaFatta,
        int? intervalloGiorni,
        bool rinunciato,
        DateTime oggi)
    {
        if (rinunciato)
        {
            return new EsitoServizioSoggiorno(StatoServizioSoggiorno.Rinunciato, intervalloGiorni, null);
        }

        if (intervalloGiorni is not { } n || n <= 0 || arrivo is not { } a || partenza is not { } p)
        {
            return new EsitoServizioSoggiorno(StatoServizioSoggiorno.NonPrevisto, intervalloGiorni, null);
        }

        var partenzaConteggio = (ultimaFatta ?? a).Date;
        var previsto = partenzaConteggio.AddDays(n);
        if (previsto >= p.Date)
        {
            return new EsitoServizioSoggiorno(StatoServizioSoggiorno.NonPrevisto, n, null);
        }

        var giorno = oggi.Date;
        var stato = giorno < previsto ? StatoServizioSoggiorno.Programmato
            : giorno == previsto ? StatoServizioSoggiorno.DaFareOggi
            : StatoServizioSoggiorno.InRitardo;

        return new EsitoServizioSoggiorno(stato, n, previsto);
    }
}
