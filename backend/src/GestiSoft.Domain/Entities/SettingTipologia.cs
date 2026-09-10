using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Tipologia di camera/appartamento (es. "Doppia vista mare"). Porta 1:1 da
/// OrderManagement.Model.BusinesObject.SettingTipology del sistema legacy.
/// </summary>
public class SettingTipologia : TenantEntity
{
    public string TipologiaCamera { get; set; } = string.Empty;

    public decimal? SpesePulizia { get; set; }

    public decimal? Animali { get; set; }

    public decimal? Cauzione { get; set; }

    public decimal? PrezzoDefault { get; set; }

    public int NumeroImplementoPersona { get; set; }

    public decimal Implemento { get; set; }

    /// <summary>
    /// Id camera lato Wubook per l'intero pool di questa Tipologia — ottenuto da new_room o
    /// associato manualmente. L'associazione OTA vive qui (non più sulla singola Camera): la
    /// quantità inviata a Wubook è il conteggio live delle Camere reali con questa TipologiaId
    /// (v. WubookCamereService), mai un numero persistito.
    /// </summary>
    public int? IdCameraWubook { get; set; }

    /// <summary>False se il pool è stato rimosso da Wubook (o mai sincronizzato) — IdCameraWubook resta valorizzato come storico.</summary>
    public bool WubookAttiva { get; set; }

    /// <summary>Codice camera Wubook (max 4 caratteri) per il pool — se non impostato, dedotto automaticamente dal nome della Tipologia.</summary>
    public string? CodiceCameraWubook { get; set; }

    /// <summary>"WooDoo CM only" per l'intero pool — si applica solo alla creazione (new_room).</summary>
    public bool WubookSoloWoodoo { get; set; }
}
