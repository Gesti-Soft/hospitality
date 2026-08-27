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
}
