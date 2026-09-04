using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Traccia un rinnovo pagato della licenza software GestiSoft di una Struttura (NON la licenza
/// Wubook — vedi <see cref="Struttura.ScadenzaLicenza"/>) — creata dal Super Admin solo quando
/// conferma esplicitamente che una modifica alla scadenza corrisponde a un pagamento reale del
/// Cliente, non ad ogni salvataggio (un rinnovo può anche essere gratuito, es. una correzione o una
/// cortesia). Alimenta la vista "incassi"/"chi deve pagare"/"chi sta per scadere" della pagina
/// Statistiche Super Admin — è il ricavo di GestiSoft stessa, distinto dall'incasso del Cliente sulle
/// proprie prenotazioni (non rilevante qui).
/// </summary>
public class RinnovoLicenza : TenantEntity
{
    /// <summary>Nuova scadenza della licenza impostata contestualmente a questo rinnovo.</summary>
    public DateTime ScadenzaImpostata { get; set; }

    /// <summary>Importo pagato dal Cliente per questo rinnovo — libero, non necessariamente la Quota annua intera (es. un rinnovo di soli 30 giorni).</summary>
    public decimal? Importo { get; set; }
}
