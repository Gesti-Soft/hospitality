using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Una prenotazione/soggiorno. Porta 1:1 da OrderManagement.Model.BusinesObject.Agenzie del
/// sistema legacy — rinominata da "Agenzie" a "Prenotazione" perché il nome legacy era fuorviante
/// (l'entità è la prenotazione, "Agenzia" è solo uno dei suoi campi: il canale di provenienza).
/// </summary>
public class Prenotazione : TenantEntity
{
    public Guid? CameraId { get; set; }

    public SettingRoom? Camera { get; set; }

    /// <summary>
    /// Tipologia (pool di camere identiche) scelta al posto di una camera specifica — permette di
    /// prenotare "una qualsiasi camera di questo tipo" lasciando che il sistema assegni la prima
    /// libera (v. AssegnazioneCameraService). Se CameraId resta nullo mentre questo è valorizzato,
    /// la prenotazione è "in attesa di assegnazione camera" (nessuna unità libera trovata al momento
    /// — capita soprattutto per un booking importato da OTA su un pool già pieno): resta comunque
    /// registrata, non persa, finché un operatore non le assegna una camera manualmente.
    /// </summary>
    public Guid? TipologiaId { get; set; }

    public SettingTipologia? Tipologia { get; set; }

    /// <summary>Scheda alloggiati del capofamiglia/ospite principale, se già compilata (vedi OspitiService). Inverso di Ospite.PrenotazioneId.</summary>
    public Ospite? Ospite { get; set; }

    /// <summary>Canale/agenzia di provenienza (es. "Booking.com", "Diretto") — testo libero, vedi SettingAgenzia.</summary>
    public string? Agenzia { get; set; }

    public string? NumeroPrenotazione { get; set; }

    public decimal? ImportoPrenotazione { get; set; }

    public decimal? ImportoPagato { get; set; }

    public decimal? ImportoTotale { get; set; }

    public DateTime? CheckIn { get; set; }

    public DateTime? CheckOut { get; set; }

    public int? NumeroOspiti { get; set; }

    public bool StatePolice { get; set; }

    public bool PMS { get; set; }

    public bool PayTourist { get; set; }

    /// <summary>
    /// I 4 toggle per prenotazione (Spese di pulizia/Animali/Cauzione/Tassa di soggiorno), fedeli al
    /// form legacy AddOrUpdateOspitiView (Animali era un ToggleButton analogo a Spese di pulizia,
    /// vedi AnimaliCheckCommand). Default true (attivi) per non alterare prenotazioni esistenti.
    /// Se TassaSoggiornoAttiva è false alla creazione, PrenotazioniService marca StatePolice/PMS/
    /// PayTourist come già "inviati" senza inviare nulla — stessa logica di
    /// AddOrUpdateOspitiViewModel.Save() del legacy (vedi commento lì per i dettagli).
    /// </summary>
    public bool TassaSoggiornoAttiva { get; set; } = true;

    public bool SpesePuliziaAttiva { get; set; } = true;

    /// <summary>
    /// A differenza degli altri toggle, default false: si applica solo se l'ospite porta
    /// effettivamente un animale, non è una spesa presente per default come pulizia/tassa.
    /// </summary>
    public bool AnimaliAttiva { get; set; }

    /// <summary>
    /// A differenza del legacy (dove la cauzione non aveva un toggle, solo un importo di sola
    /// visualizzazione), qui è un quarto toggle a richiesta esplicita dell'utente, per coerenza visiva
    /// con gli altri — nessun effetto sugli invii, solo sull'importo cauzione applicato.
    /// </summary>
    public bool CauzioneAttiva { get; set; } = true;

    public string? CheckEditSelection { get; set; }

    public string? Supply { get; set; }

    public int Anno { get; set; } = DateTime.UtcNow.Year;

    public decimal? TotalTax { get; set; }

    public StatoPrenotazione? StatoPrenotazione { get; set; }

    /// <summary>
    /// Id prenotazione lato Wubook (rcode/reservation_code), usato come chiave di deduplica nel
    /// pull da fetch_new_bookings/fetch_booking. Il legacy deduplicava per valore su
    /// NumeroPrenotazione (stringa, ChannelReservationCode) — fragile perché quel campo è anche il
    /// numero mostrato all'operatore e può collidere tra canali diversi. Null per le prenotazioni
    /// create manualmente/non da OTA.
    /// </summary>
    public int? IdPrenotazioneWubook { get; set; }
}
