using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Configurazione condivisa PayTourist per una Struttura (una riga per Struttura) — porta le voci
/// <c>GeneralSetting</c> "TOKEN PAYTOURIST"/"PORTALE ONLINE" del legacy.
/// A differenza di Wubook/Alloggiati Web/Osservatorio, qui NON viene salvato nessun "id software":
/// su istruzione esplicita dell'utente, l'Id Software PayTourist (<c>software_id</c> nelle chiamate
/// API) non va mai configurato manualmente — viene sempre letto al volo da gestisoft.it tramite la
/// stessa chiamata di rinnovo licenza già usata per Wubook (vedi
/// <see cref="WubookIntegrazione.IdPaytouristCache"/>/WubookLicenzaService.GetIdPaytouristAsync).
/// Anche il campo <c>Utente</c> del legacy (<c>PayTouristUser.Utente</c>) non è portato: letto nel
/// codice legacy, risulta dichiarato ma mai inviato in nessuna chiamata verso l'API PayTourist —
/// campo morto, non riportato qui.
/// </summary>
public class PayTouristIntegrazione : TenantEntity
{
    /// <summary>Bearer token verso l'API PayTourist, inserito dall'operatore — segreto, non va mai esposto in risposta API.</summary>
    public string? Token { get; set; }

    /// <summary>
    /// Se attivo, l'invio arricchisce ogni prenotazione con i dati del portale online di
    /// prenotazione (abbinato per nome canale a <see cref="Prenotazione.Agenzia"/>) — porta
    /// "PORTALE ONLINE (0 = NO, 1 = SI)" del legacy.
    /// </summary>
    public bool PortaleOnlineAttivo { get; set; }
}
