using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public interface IWubookEventoRicevutoRepository
{
    Task<WubookEventoRicevuto?> GetByRcodeAsync(Guid strutturaId, int rcode, CancellationToken cancellationToken);

    /// <summary>Più recenti prima — usata dalla pagina Wubook per mostrare le prenotazioni intercettate.</summary>
    Task<IReadOnlyList<WubookEventoRicevuto>> ListByStrutturaIdAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Avvisi diretti dell'OTA da elaborare adesso (non in attesa di un nuovo tentativo), dal più vecchio — sola lettura.</summary>
    Task<IReadOnlyList<WubookEventoRicevuto>> ListDaElaborareAsync(Guid strutturaId, DateTime adessoUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Avviso ricevuto: in un'unica istruzione inserisce la riga o, se c'è già, la rimette "da
    /// elaborare" subito. Atomica e sempre scritta: un avviso arrivato mentre il Worker elabora il
    /// precedente della stessa prenotazione resta in coda e viene rielaborato.
    /// </summary>
    Task SegnaDaElaborareAsync(Guid strutturaId, int rcode, string lcode, DateTime adessoUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Il Worker prende in carico l'avviso togliendo il segno "da elaborare", solo se c'è ancora.
    /// False = qualcun altro l'ha già preso. Un nuovo avviso arrivato dopo rimette il segno.
    /// </summary>
    Task<bool> PrendiInCaricoAsync(Guid eventoId, CancellationToken cancellationToken);

    /// <summary>
    /// Esito dell'elaborazione. Il segno "da elaborare" si rimette solo per riprovare
    /// (<paramref name="riprovaDopoUtc"/> valorizzato) e non si toglie mai qui: lo toglie solo la presa in carico.
    /// </summary>
    Task RegistraEsitoAsync(Guid eventoId, string? errore, int tentativi, DateTime? riprovaDopoUtc, DateTime adessoUtc, CancellationToken cancellationToken);

    /// <summary>Inserisce l'evento, o aggiorna quello già presente per lo stesso Rcode/Struttura se è un retry.</summary>
    Task UpsertAsync(WubookEventoRicevuto evento, CancellationToken cancellationToken);
}
