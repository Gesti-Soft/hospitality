namespace GestiSoft.Application.Statistiche;

/// <summary>
/// Aggregazioni per la pagina Statistiche di una Struttura, porta le formule di
/// DashBoardViewModel.cs del gestionale legacy (WPF) — a differenza del legacy, che caricava tutte
/// le prenotazioni dell'anno in memoria e aggregava lì (dataset single-tenant piccolo), qui ogni
/// metodo traduce l'aggregazione in SQL (GroupBy/Sum/Average via EF Core), mai un
/// `.ToList()` di righe intere prima di aggregare.
/// </summary>
public interface IStatisticheRepository
{
    /// <summary>Anni (Prenotazione.Anno) con almeno una prenotazione per questa Struttura, ordinati dal più recente — usati per popolare il selettore anno senza proporre anni sicuramente vuoti.</summary>
    Task<IReadOnlyList<int>> ListaAnniConDatiAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Conteggio prenotazioni dell'anno, nessun filtro sullo stato (fedele al legacy, che non ne applicava).</summary>
    Task<int> ContaPrenotazioniAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    Task<(decimal Stimato, decimal Effettivo)> SommaImportiAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>Media di Ospite.Permanenza (notti) tra le prenotazioni dell'anno con Permanenza &gt; 0; null se nessuna.</summary>
    Task<double?> PermanenzaMediaAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>Somma di Ospite.Permanenza sullo stesso insieme (Permanenza &gt; 0) usato per la permanenza media — è il numeratore del tasso di occupazione.</summary>
    Task<int> SommaPermanenzaAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    Task<IReadOnlyList<(string? Etichetta, int Conteggio)>> ContaPerAgenziaAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>Nazionalità (Ospite.Cittadinanza) dell'ospite principale 1:1 collegato alla prenotazione, filtrato per anno della prenotazione.</summary>
    Task<IReadOnlyList<(string? Etichetta, int Conteggio)>> ContaPerNazionalitaAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>Sum(ImportoPagato) per mese di CheckIn, solo i mesi con almeno una riga (il chiamante completa a 12 valori).</summary>
    Task<IReadOnlyList<(int Mese, decimal Totale)>> RicavoMensileAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    Task<IReadOnlyList<(string Tipologia, decimal Totale)>> RicavoPerTipologiaAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    /// <summary>Righe grezze (tipologia, mese, conteggio) — il chiamante le dispone in matrice su TUTTE le tipologie della struttura, non solo quelle presenti qui.</summary>
    Task<IReadOnlyList<(string Tipologia, int Mese, int Conteggio)>> PrenotazioniPerTipologiaMeseAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    Task<decimal> SommaTassaSoggiornoAnnoAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);

    Task<IReadOnlyList<(int Mese, decimal Totale)>> TassaSoggiornoMensileAsync(Guid strutturaId, int anno, CancellationToken cancellationToken);
}
