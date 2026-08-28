using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Una camera/appartamento della struttura. Porta 1:1 da
/// OrderManagement.Model.BusinesObject.SettingRooms del sistema legacy (il campo di testo libero
/// "Tipologia" del legacy, ridondante con la FK, non viene riportato: il nome tipologia si ottiene
/// tramite query su TipologiaId).
/// </summary>
public class SettingRoom : TenantEntity
{
    public Guid? TipologiaId { get; set; }

    public SettingTipologia? Tipologia { get; set; }

    public StatoCamera StateRoom { get; set; }

    public string Nome { get; set; } = string.Empty;

    public int? CapacitaOspiti { get; set; }

    public int? SoggiornoMinimo { get; set; }

    /// <summary>
    /// Id camera lato Wubook, ottenuto da new_room o associato manualmente. Nel legacy questa
    /// associazione viveva in una tabella separata (CamereAssociate) con un flag Active per poter
    /// disattivare senza perdere lo storico; qui, essendo comunque scoped per Struttura/tenant,
    /// un campo diretto con un flag di stato è sufficiente e più semplice da interrogare.
    /// </summary>
    public int? IdCameraWubook { get; set; }

    /// <summary>False se la camera è stata rimossa da Wubook (o mai sincronizzata) — IdCameraWubook resta valorizzato come storico.</summary>
    public bool WubookAttiva { get; set; }

    /// <summary>Codice camera Wubook (max 4 caratteri) — se non impostato, dedotto automaticamente dal nome (v. WubookCamereService.ShortNameDa).</summary>
    public string? CodiceCameraWubook { get; set; }

    /// <summary>Prezzo di default inviato a Wubook per questa camera — se non impostato, usa SettingTipologia.PrezzoDefault (condiviso da tutte le camere della tipologia).</summary>
    public decimal? PrezzoWubookOverride { get; set; }

    /// <summary>
    /// "WooDoo CM only": camera visibile solo dal channel manager, non vendibile sul motore di
    /// prenotazione Wubook. Si applica solo alla creazione (new_room) — modificarlo su una camera
    /// già associata richiede rimuovere e ri-sincronizzare (v. WubookCamereService, il parametro
    /// equivalente su mod_room è opzionale e finale, richiede altri campi mai tracciati qui).
    /// </summary>
    public bool WubookSoloWoodoo { get; set; }
}
