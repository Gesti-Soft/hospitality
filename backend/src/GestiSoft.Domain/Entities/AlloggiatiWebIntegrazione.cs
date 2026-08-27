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
public class AlloggiatiWebIntegrazione : TenantEntity
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
}
