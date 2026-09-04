namespace GestiSoft.Application.Statistiche;

/// <summary>
/// Formula del tasso di occupazione, porta 1:1 DashBoardViewModel.CalcolaKpi del gestionale legacy
/// (WPF): notti totali soggiornate / (numero camere × giorni trascorsi nell'anno) × 100, cappata al
/// 100%. Il legacy assumeva sempre "anno corrente" (DateTime.Now.DayOfYear); qui l'anno è
/// selezionabile in UI, quindi va gestito anche un anno passato (giorni totali dell'anno, 365/366)
/// e un anno futuro (0 giorni trascorsi, nessuna divisione per zero). Riusata sia dalla pagina
/// Statistiche per Struttura sia dalla classifica occupazione della pagina Statistiche Super Admin,
/// per non duplicare la regola anno-corrente/anno-passato/anno-futuro in due punti.
/// </summary>
public static class CalcoloOccupazione
{
    public static int GiorniTrascorsiAnno(int anno)
    {
        var annoCorrente = DateTime.UtcNow.Year;
        if (anno == annoCorrente)
        {
            return DateTime.UtcNow.DayOfYear;
        }

        return anno < annoCorrente ? (DateTime.IsLeapYear(anno) ? 366 : 365) : 0;
    }

    public static double TassoOccupazionePercentuale(int sommaPermanenzaNotti, int numeroCamere, int anno)
    {
        var giorniTrascorsi = GiorniTrascorsiAnno(anno);
        if (numeroCamere <= 0 || giorniTrascorsi <= 0)
        {
            return 0;
        }

        var percentuale = (double)sommaPermanenzaNotti / (numeroCamere * giorniTrascorsi) * 100;
        return Math.Min(100, Math.Round(percentuale, 1));
    }
}
