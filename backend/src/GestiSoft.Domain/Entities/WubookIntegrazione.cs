using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Configurazione e stato dell'integrazione Wubook per una Struttura (una riga per Struttura).
/// Separata da <see cref="ImpostazioniStruttura"/> perché mescola due cose scritte da chi/quando
/// diversi: <see cref="GestisoftUsername"/>/<see cref="GestisoftToken"/> sono la licenza
/// gestisoft.it inserita dall'operatore via UI, mentre <see cref="ApiKeyCache"/>/
/// <see cref="LcodeCache"/>/<see cref="CacheAggiornataAtUtc"/> sono scritti dal job periodico di
/// rinnovo (vedi WubookLicenzaService) — tenerli sullo stesso record di ImpostazioniStruttura
/// avrebbe rischiato scritture concorrenti che si sovrascrivono a vicenda.
/// Porta la logica di GestiCache/set-running del sistema legacy: nel legacy la licenza
/// (username+token) era una sola per installazione (single-tenant); qui è per Struttura, decisione
/// presa esplicitamente con l'utente. tokenWb/idWoBook non vengono MAI presi da configurazione
/// locale: arrivano sempre, ad ogni rinnovo, dalla risposta di gestisoft.it/users/set-running.
/// </summary>
public class WubookIntegrazione : TenantEntity
{
    public bool Attivo { get; set; }

    /// <summary>Username della licenza gestisoft.it di questa Struttura.</summary>
    public string? GestisoftUsername { get; set; }

    /// <summary>Token della licenza gestisoft.it di questa Struttura (segreto, non va mai esposto in risposta API).</summary>
    public string? GestisoftToken { get; set; }

    /// <summary>Apikey Wubook (tokenWb) — cache dell'ultimo valore ricevuto da gestisoft.it, mai inserita a mano.</summary>
    public string? ApiKeyCache { get; set; }

    /// <summary>Lcode Wubook (idWoBook) — cache dell'ultimo valore ricevuto da gestisoft.it, mai inserito a mano.</summary>
    public string? LcodeCache { get; set; }

    public DateTime? CacheAggiornataAtUtc { get; set; }

    /// <summary>Ultimo errore di rinnovo licenza/credenziali, per mostrarlo in UI (es. "Token scaduto", "Abbonamento scaduto").</summary>
    public string? UltimoErrore { get; set; }
}
