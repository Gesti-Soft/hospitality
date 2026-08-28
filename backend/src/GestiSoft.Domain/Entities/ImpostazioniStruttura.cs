using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Impostazioni di una Struttura: feature toggle per le integrazioni esterne e parametri di
/// invio. Sostituisce il magazzino chiave/valore generico GeneralSetting del legacy (gestito da
/// Controller) con colonne tipizzate — più semplice da validare e da esporre in UI.
/// </summary>
public class ImpostazioniStruttura : TenantEntity
{
    public bool PoliziaStatoAttiva { get; set; }

    public bool OsservatorioAttivo { get; set; }

    public bool PayTouristAttivo { get; set; }

    /// <summary>Orario giornaliero di invio schedine/Osservatorio/PayTourist (ora locale struttura).</summary>
    public TimeOnly? OraInvioGiornaliero { get; set; }

    public decimal? TassaSoggiornoPrezzo { get; set; }

    public int? TassaSoggiornoMaxGiorni { get; set; }

    /// <summary>
    /// Comune dove opera fisicamente la struttura (porta GeneralSetting["COMUNE ATTIVITA'"] del
    /// legacy) — distinto dal comune fiscale/amministrativo dell'azienda (DatiAziendali.Comune,
    /// Fase 4): usato per l'esenzione tassa di soggiorno per residenza e per le riduzioni
    /// PayTourist per residenza/esenzione (StatePoliceLogic.ControlReduction nel legacy).
    /// </summary>
    public string? ComuneAttivita { get; set; }
}
