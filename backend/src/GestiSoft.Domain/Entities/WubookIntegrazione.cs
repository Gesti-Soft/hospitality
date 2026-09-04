using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Configurazione e stato dell'integrazione Wubook per una Struttura (una riga per Struttura).
/// <see cref="Attivo"/> è un toggle self-service del Cliente/operatore (richiede SettingRoomWrite);
/// <see cref="CodiceStruttura"/> è invece gestito solo dal Super Admin, perché è GestiSoft (l'azienda,
/// non il singolo cliente) il vero titolare dell'account partner Wubook. <see cref="GestisoftUsername"/>/
/// <see cref="GestisoftToken"/> restano invece solo per identificarsi verso gestisoft.it nel polling
/// minute-by-minute delle prenotazioni (Wubook notifica le nuove prenotazioni a gestisoft.it, non
/// qui). NOTA: la scadenza della licenza NON è qui — è <see cref="Struttura.ScadenzaLicenza"/>, la
/// licenza software GestiSoft concessa al Cliente, indipendente da Wubook.
/// </summary>
public class WubookIntegrazione : TenantEntity
{
    public bool Attivo { get; set; }

    /// <summary>Username verso gestisoft.it, usato solo per il polling eventi (vedi WubookEventiService) — gestito dal Super Admin.</summary>
    public string? GestisoftUsername { get; set; }

    /// <summary>Token verso gestisoft.it, usato solo per il polling eventi (segreto, non va mai esposto in risposta API) — gestito dal Super Admin.</summary>
    public string? GestisoftToken { get; set; }

    /// <summary>Codice struttura Wubook (lcode) — inserito a mano dal Super Admin, non più recuperato da gestisoft.it.</summary>
    public string? CodiceStruttura { get; set; }

    /// <summary>Ultimo motivo per cui le credenziali non sono utilizzabili (mancanti o la licenza della Struttura è scaduta), per mostrarlo in UI.</summary>
    public string? UltimoErrore { get; set; }
}
