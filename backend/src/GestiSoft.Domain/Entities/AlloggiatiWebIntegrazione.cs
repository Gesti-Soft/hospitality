using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Credenziali e stato dell'invio giornaliero "Alloggiati Web" (Polizia di Stato) per una
/// Struttura (una riga per Struttura). L'attivazione/disattivazione del servizio e l'orario di
/// invio restano su <see cref="ImpostazioniStruttura"/> (PoliziaStatoAttiva/OraInvioGiornaliero,
/// già presenti da Fase 2) — qui vivono solo le credenziali verso il servizio SOAP esterno
/// (Utente/Password/WsKey, porta 1:1 SyncStatePolice.Model.Model.Settings del legacy, dove erano
/// una tabella per singola installazione single-tenant) e lo stato dell'ultimo invio, per evitare
/// di rieseguire il batch più volte nella stessa giornata se il job Quartz gira più volte prima
/// che <see cref="UltimoInvioAtUtc"/> venga aggiornato.
/// </summary>
public class AlloggiatiWebIntegrazione : TenantEntity, IStatoTentativi
{
    public string? Utente { get; set; }

    /// <summary>Password Alloggiati Web (segreta, non va mai esposta in risposta API) — richiesta dal protocollo SOAP ad ogni GenerateToken, non è una nostra credenziale di autenticazione.</summary>
    public string? Password { get; set; }

    /// <summary>WSKey assegnata dalla Questura al software per questa struttura.</summary>
    public string? WsKey { get; set; }

    public DateTime? UltimoInvioAtUtc { get; set; }

    public int? UltimeSchedineInviate { get; set; }

    /// <summary>Ultimo errore di invio, per mostrarlo in UI (null se l'ultimo invio è andato a buon fine).</summary>
    public string? UltimoErrore { get; set; }

    /// <summary>
    /// Tentativi di invio già falliti nella giornata indicata da <see cref="TentativiGiornoAtUtc"/>,
    /// con il momento in cui è lecito riprovare: insieme limitano i giri a vuoto del job, che gira
    /// ogni minuto fino a mezzanotte (vedi <see cref="PoliticaTentativi"/>). Azzerati da un invio
    /// riuscito e, da soli, dal cambio di giorno.
    /// </summary>
    public int TentativiFallitiOggi { get; set; }

    public DateTime? TentativiGiornoAtUtc { get; set; }

    public DateTime? ProssimoTentativoAtUtc { get; set; }

    /// <summary>Vero se l'ultimo fallimento era di configurazione (credenziali, associazioni): vale un solo tentativo al giorno, ritentarlo stasera non può riuscire.</summary>
    public bool UltimoErroreDefinitivo { get; set; }

    /// <summary>
    /// Ultimo test di connessione (GenerateToken, nessuna schedina inviata) riuscito, eseguito al
    /// salvataggio delle credenziali — vedi AlloggiatiWebConfigService.VerificaConnessioneAsync.
    /// Conta come "attivo" nella dashboard Stato invii automatici anche prima del primo invio
    /// giornaliero reale (<see cref="UltimoInvioAtUtc"/>), su richiesta esplicita dell'utente.
    /// </summary>
    public DateTime? UltimaVerificaOkAtUtc { get; set; }
}
