using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Una "struttura" PayTourist (porta 1:1 <c>SyncStatePolice.Model.Model.PayTourist.PayTouristUser</c>
/// del legacy) — una Struttura di questo gestionale può averne più di una, ognuna con il proprio
/// <see cref="IdStrutturaPaytourist"/> (il <c>structure_id</c> richiesto dall'API PayTourist) e un
/// sottoinsieme di tipologie camera instradate, esattamente come <c>OsservatorioAppartamento</c>
/// (Fase 7) instrada le tipologie verso più "appartamenti". Stessa correzione deliberata già fatta
/// lì: il legacy associava le tipologie con una stringa CSV di id (<c>IdTipology</c>) — qui una vera
/// relazione many-to-many (<see cref="Tipologie"/>).
/// Il campo <c>Licenza</c> del legacy (intestazione a 196 caratteri usata solo nel generatore
/// dell'export testuale locale per singola prenotazione) non è portato: l'export di questo sistema
/// (vedi PayTouristInvioService.EsportaAsync) riusa la stessa costruzione dati dell'invio via API,
/// non un tracciato a colonne fisse separato.
/// </summary>
public class PayTouristStruttura : TenantEntity
{
    /// <summary>Etichetta libera per distinguere più strutture PayTourist nella UI (il legacy le identificava solo per Id numerico).</summary>
    public string? Nome { get; set; }

    /// <summary>Id "structure_id" lato PayTourist, inserito dall'operatore — non deducibile da nessun'altra fonte.</summary>
    public int? IdStrutturaPaytourist { get; set; }

    public DateTime? UltimoInvioAtUtc { get; set; }

    public int? UltimeInviate { get; set; }

    public string? UltimoErrore { get; set; }

    public ICollection<PayTouristStrutturaTipologia> Tipologie { get; set; } = new List<PayTouristStrutturaTipologia>();
}
