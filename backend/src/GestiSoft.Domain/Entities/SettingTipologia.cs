using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

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
    /// Come si legge <see cref="Implemento"/>: euro a notte, oppure percentuale del prezzo della camera
    /// per quella notte. Si salva con la riduzione per ospite in meno, non dal form generale (che la
    /// pagina OTA rimanda con un elenco fisso di campi e lo riporterebbe a euro).
    /// </summary>
    public TipoVariazionePrezzo TipoImplemento { get; set; } = TipoVariazionePrezzo.Euro;

    /// <summary>
    /// Riduzione a notte per ogni ospite in meno rispetto a <see cref="NumeroImplementoPersona"/>
    /// (es. doppia a uso singola), come il prezzo derivato per occupazione di Booking. Facoltativa:
    /// null o 0 = nessuna riduzione, il prezzo resta quello degli ospiti inclusi. I bambini contano
    /// come ospiti. Si salva da un endpoint suo, come le pulizie.
    /// </summary>
    public decimal? RiduzioneOspiteInMeno { get; set; }

    /// <inheritdoc cref="RiduzioneOspiteInMeno"/>
    public TipoVariazionePrezzo TipoRiduzioneOspiteInMeno { get; set; } = TipoVariazionePrezzo.Euro;

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

    /// <summary>
    /// Pulizia durante il soggiorno per questa tipologia: null = come la struttura, 0 = nessuna, N =
    /// ogni N giorni. Non passa dal form generale della tipologia (che la pagina OTA rimanda con un
    /// elenco fisso di campi) ma da un endpoint suo, così nessun salvataggio altrui la azzera.
    /// </summary>
    public int? IntervalloPuliziaGiorni { get; set; }

    /// <inheritdoc cref="IntervalloPuliziaGiorni"/>
    public int? IntervalloBiancheriaGiorni { get; set; }
}
