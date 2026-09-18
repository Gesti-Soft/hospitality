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
    /// Riduzione automatica per età (in aggiunta a residenza ed "Esente da tassa" manuale) — null/non
    /// impostato = nessuna riduzione automatica per quella fascia. I valori esatti dipendono dal
    /// regolamento del singolo Comune: se PayTourist è attivo per la struttura vengono proposti in
    /// automatico da GET api/v1/reductions (vedi PayTouristConfigService.SuggerisciEtaEsenzioneTassaAsync
    /// — l'API non ha un campo età strutturato, solo testo libero diverso per Comune, l'estrazione è
    /// un'euristica), altrimenti vanno impostati a mano. Sotto questa età, riduzione applicata
    /// ("entro il compimento").
    /// </summary>
    public int? TassaSoggiornoEtaEsenzioneMinori { get; set; }

    /// <summary>Da questa età in su, riduzione applicata ("dal compimento") — vedi <see cref="TassaSoggiornoEtaEsenzioneMinori"/>.</summary>
    public int? TassaSoggiornoEtaEsenzioneAnziani { get; set; }

    /// <summary>
    /// Percentuale di riduzione (0-100) per un ospite residente nel Comune Attività — non è detto
    /// sia sempre esenzione piena: un Comune può configurare uno sconto parziale invece del 100%.
    /// Null = nessuna riduzione automatica per residenza (il campo compare solo se
    /// <see cref="ComuneAttivita"/> è configurato).
    /// </summary>
    public decimal? TassaSoggiornoPercentualeResidenti { get; set; }

    /// <summary>Percentuale di riduzione (0-100) per la fascia minori — vedi <see cref="TassaSoggiornoEtaEsenzioneMinori"/>; null = 100% (esenzione piena, comportamento storico) se la soglia età è comunque impostata.</summary>
    public decimal? TassaSoggiornoPercentualeMinori { get; set; }

    /// <summary>Percentuale di riduzione (0-100) per la fascia anziani — vedi <see cref="TassaSoggiornoEtaEsenzioneAnziani"/>; null = 100% (esenzione piena, comportamento storico) se la soglia età è comunque impostata.</summary>
    public decimal? TassaSoggiornoPercentualeAnziani { get; set; }

    /// <summary>
    /// Comune dove opera fisicamente la struttura (porta GeneralSetting["COMUNE ATTIVITA'"] del
    /// legacy) — distinto dal comune fiscale/amministrativo dell'azienda (DatiAziendali.Comune,
    /// Fase 4): usato per l'esenzione tassa di soggiorno per residenza e per le riduzioni
    /// PayTourist per residenza/esenzione (StatePoliceLogic.ControlReduction nel legacy).
    /// </summary>
    public string? ComuneAttivita { get; set; }

}
