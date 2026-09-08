using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Configurazione di una "entità" (appartamento/PMS unit) Osservatorio Turistico per una
/// Struttura — porta 1:1 <c>SyncStatePolice.Model.Model.Osservatorio.Appartementi</c> del legacy,
/// con due correzioni deliberate rispetto ad esso:
/// 1. Il legacy teneva il cursore di chiusura giornata (<c>CurrentData</c>) e il contatore
///    progressivo StayId (<c>StayId</c>) come DUE RIGHE GLOBALI UNICHE nel database, condivise da
///    TUTTI gli appartamenti configurati — un bug reale anche nel legacy stesso (con più
///    appartamenti configurati, chiudere un giorno per uno avrebbe fatto avanzare il cursore anche
///    per gli altri). Qui <see cref="CursoreDataAtUtc"/>/<see cref="ProssimoStayIdProgressivo"/>
///    sono per Appartamento (oltre che per Struttura, essendo multi-tenant).
/// 2. Il legacy associava un appartamento alle tipologie camera tramite una stringa CSV di soli
///    id (<c>IdAppartamento</c>, es. "3, 7, 12") — qui sostituita da una vera relazione
///    many-to-many (<see cref="Tipologie"/>), coerente con la rimozione di campi testo libero
///    ridondanti già fatta per altre entità in Fase 1.
/// </summary>
public class OsservatorioAppartamento : TenantEntity
{
    /// <summary>Etichetta libera per distinguere più appartamenti nella UI (il legacy non ne aveva una, identificava gli appartamenti solo per Id numerico).</summary>
    public string? Nome { get; set; }

    /// <summary>Quale sistema regionale contattare per questo appartamento — vedi ProviderOsservatorio. Un solo valore possibile oggi, il campo esiste già per non dover toccare schema/dati quando si aggiungerà il secondo.</summary>
    public ProviderOsservatorio Provider { get; set; } = ProviderOsservatorio.Sicilia;

    public string? EntityCode { get; set; }

    /// <summary>Segreta, non va mai esposta in risposta API.</summary>
    public string? Password { get; set; }

    public string? HotelCode { get; set; }

    /// <summary>Prossimo giorno da chiudere (enddayfrompms) per questo appartamento. Null = nessun invio ancora effettuato, si parte da oggi senza recuperare arretrati.</summary>
    public DateTime? CursoreDataAtUtc { get; set; }

    public int ProssimoStayIdProgressivo { get; set; } = 1;

    public DateTime? UltimoInvioAtUtc { get; set; }

    public int? UltimeSchedineInviate { get; set; }

    public string? UltimoErrore { get; set; }

    /// <summary>
    /// Ultimo test di connessione (Login + GetCurrentStatusDate, nessuna Stay inviata) riuscito,
    /// eseguito al salvataggio — vedi OsservatorioConfigService.VerificaConnessioneAsync. Conta come
    /// "attivo" nella dashboard Stato invii automatici anche prima del primo invio giornaliero
    /// reale (<see cref="UltimoInvioAtUtc"/>), su richiesta esplicita dell'utente.
    /// </summary>
    public DateTime? UltimaVerificaOkAtUtc { get; set; }

    public ICollection<OsservatorioAppartamentoTipologia> Tipologie { get; set; } = new List<OsservatorioAppartamentoTipologia>();
}
