namespace GestiSoft.Domain.Entities;

/// <summary>
/// Quante volte, e con che ritmo, un invio giornaliero alle PA (Polizia di Stato, Osservatorio
/// Turistico, PayTourist) può essere ritentato nella stessa giornata.
///
/// I tre job girano ogni minuto dall'orario configurato in poi. Senza un limite, un errore che
/// dura tutta la sera produce un tentativo al minuto fino a mezzanotte, **per ogni appartamento o
/// integrazione**: con molte strutture configurate diventano migliaia di chiamate a un portale che
/// è già in difficoltà, e centinaia di righe di log identiche che coprono quelle vere.
///
/// Il ritardo raddoppia ad ogni fallimento (1, 2, 4, 8, 16, 32 minuti): sei tentativi coprono
/// un'ora piena — quanto basta a superare una manutenzione serale del portale — con sei chiamate
/// invece di sessanta. A intervallo fisso si otterrebbe il contrario: o si spreca (ogni minuto) o
/// si copre una finestra troppo corta per servire a qualcosa.
///
/// Non tutti gli errori meritano un tentativo. Quelli di configurazione (credenziali mancanti,
/// nessuna tipologia associata, id struttura non impostato) non si risolvono da soli: valgono un
/// solo giro al giorno, con la notifica che chiede di andare a sistemarli. Vedi
/// <see cref="EsitoTentativo"/>, deciso dal punto del codice in cui l'errore nasce e non dal testo
/// del messaggio, che cambierebbe senza preavviso.
/// </summary>
public static class PoliticaTentativi
{
    /// <summary>Tentativi al giorno per un errore che ha senso ritentare. Il sesto ritardo (32') porta il totale oltre l'ora.</summary>
    public const int MassimoTentativiRitentabili = 6;

    /// <summary>Un errore di configurazione si riprova il giorno dopo, non prima: ritentarlo stasera non può riuscire.</summary>
    public const int MassimoTentativiDefinitivi = 1;

    public static readonly TimeSpan RitardoIniziale = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Attesa prima del prossimo tentativo, dopo <paramref name="tentativiFalliti"/> fallimenti:
    /// 1, 2, 4, 8, 16, 32 minuti. Il conteggio parte da 1 (primo fallimento → 1 minuto).
    /// </summary>
    public static TimeSpan RitardoDopo(int tentativiFalliti) =>
        tentativiFalliti <= 1
            ? RitardoIniziale
            : RitardoIniziale * Math.Pow(2, Math.Min(tentativiFalliti, MassimoTentativiRitentabili) - 1);

    /// <summary>
    /// Se questo giro del job deve processare l'elemento (appartamento o integrazione) oppure
    /// saltarlo. Si salta quando i tentativi della giornata sono esauriti o quando l'attesa dopo
    /// l'ultimo fallimento non è ancora trascorsa. Il contatore vale per un solo giorno: quando
    /// <paramref name="giornoDeiTentativi"/> non è più oggi riparte da zero, così il giorno dopo si
    /// ricomincia sempre — anche dopo un errore definitivo.
    /// </summary>
    public static bool PuoTentare(
        int tentativiFalliti,
        DateTime? giornoDeiTentativi,
        DateTime? prossimoTentativoAtUtc,
        bool ultimoErroreDefinitivo,
        DateTime adessoUtc)
    {
        if (giornoDeiTentativi?.Date != adessoUtc.Date)
        {
            return true;
        }

        var massimo = ultimoErroreDefinitivo ? MassimoTentativiDefinitivi : MassimoTentativiRitentabili;
        if (tentativiFalliti >= massimo)
        {
            return false;
        }

        return prossimoTentativoAtUtc is not { } prossimo || adessoUtc >= prossimo;
    }

    /// <summary>
    /// Vero quando questo fallimento è l'ultimo della giornata: è il momento in cui scrivere la riga
    /// di riepilogo nel Log e creare la notifica. Segnalare ad ogni tentativo riempirebbe il Log
    /// dello stesso rumore che il limite serve a togliere.
    /// </summary>
    public static bool EsauritiDopo(int tentativiFalliti, bool erroreDefinitivo) =>
        tentativiFalliti >= (erroreDefinitivo ? MassimoTentativiDefinitivi : MassimoTentativiRitentabili);

    /// <summary>Come sopra, sullo stato salvato dell'elemento.</summary>
    public static bool PuoTentare(IStatoTentativi stato, DateTime adessoUtc) =>
        PuoTentare(stato.TentativiFallitiOggi, stato.TentativiGiornoAtUtc, stato.ProssimoTentativoAtUtc, stato.UltimoErroreDefinitivo, adessoUtc);

    /// <summary>
    /// Invio già andato a buon fine oggi: il contatore è di oggi e non registra fallimenti. Serve ai
    /// job per non rifare un lavoro già concluso, distinguendolo da "oggi ha solo fallito".
    /// </summary>
    public static bool GiaRiuscitoOggi(IStatoTentativi stato, DateTime adessoUtc) =>
        stato.TentativiGiornoAtUtc?.Date == adessoUtc.Date && stato.TentativiFallitiOggi == 0;

    /// <summary>
    /// Aggiorna il contatore dopo un tentativo e dice se è il momento di segnalare la resa — vero
    /// **solo** al fallimento che esaurisce la giornata, così log e notifica escono una volta sola
    /// invece che ad ogni giro.
    /// </summary>
    public static bool RegistraEsito(IStatoTentativi stato, EsitoTentativo esito, DateTime adessoUtc)
    {
        if (stato.TentativiGiornoAtUtc?.Date != adessoUtc.Date)
        {
            stato.TentativiFallitiOggi = 0;
            stato.TentativiGiornoAtUtc = adessoUtc.Date;
        }

        if (esito == EsitoTentativo.Riuscito)
        {
            stato.TentativiFallitiOggi = 0;
            stato.ProssimoTentativoAtUtc = null;
            stato.UltimoErroreDefinitivo = false;
            return false;
        }

        var definitivo = esito == EsitoTentativo.ErroreDefinitivo;
        stato.TentativiFallitiOggi++;
        stato.UltimoErroreDefinitivo = definitivo;
        stato.ProssimoTentativoAtUtc = adessoUtc + RitardoDopo(stato.TentativiFallitiOggi);

        return EsauritiDopo(stato.TentativiFallitiOggi, definitivo);
    }
}

/// <summary>
/// Stato dei tentativi di un elemento che riceve invii giornalieri (un appartamento Osservatorio,
/// l'integrazione Alloggiati Web di una Struttura, una struttura PayTourist). Sta su un'interfaccia
/// perché la regola è la stessa per tutti e tre, e tenerla in un punto solo evita che i tre job
/// divergano su quante volte ritentano — differenza che si noterebbe solo il giorno in cui un
/// portale è giù.
/// </summary>
public interface IStatoTentativi
{
    int TentativiFallitiOggi { get; set; }
    DateTime? TentativiGiornoAtUtc { get; set; }
    DateTime? ProssimoTentativoAtUtc { get; set; }
    bool UltimoErroreDefinitivo { get; set; }
}

/// <summary>
/// Esito di un tentativo di invio, dal punto di vista di "vale la pena riprovare stasera?".
/// Lo decide il punto del codice che rileva l'errore, non un'analisi del messaggio.
/// </summary>
public enum EsitoTentativo
{
    /// <summary>Andato a buon fine: il contatore della giornata si azzera.</summary>
    Riuscito = 0,

    /// <summary>Configurazione mancante o incoerente: ritentare stasera non può cambiare nulla.</summary>
    ErroreDefinitivo = 1,

    /// <summary>Rete, timeout, portale che risponde male: ha senso riprovare tra poco.</summary>
    ErroreRitentabile = 2,
}
