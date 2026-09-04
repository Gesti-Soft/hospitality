namespace GestiSoft.Domain.Enums;

/// <summary>
/// Stato aggregato di una integrazione esterna (Alloggiati Web/Osservatorio/PayTourist/Wubook) per
/// una Struttura, usato dalla dashboard Statistiche Super Admin per riassumere in un solo valore
/// gli stessi campi (UltimoErrore/UltimoInvioAtUtc/UltimaVerificaOkAtUtc) già mostrati uno per uno
/// in DashboardPage.tsx per la singola struttura dell'utente loggato.
/// </summary>
public enum EsitoIntegrazione
{
    /// <summary>Il Super Admin non ha concesso questo servizio a questa Struttura.</summary>
    NonConcesso = 1,

    /// <summary>Servizio concesso ma senza credenziali/configurazione salvate.</summary>
    NonConfigurato = 2,

    /// <summary>Configurato ma senza ancora un invio o una verifica di connessione riusciti.</summary>
    Attesa = 3,

    /// <summary>Ultimo invio o verifica di connessione falliti.</summary>
    Errore = 4,

    /// <summary>Almeno un invio o una verifica di connessione riusciti, nessun errore più recente.</summary>
    Attivo = 5,
}
