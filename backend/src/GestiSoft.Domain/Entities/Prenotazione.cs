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

    /// <summary>
    /// Momento reale dell'arrivo dell'ospite, valorizzato da PrenotazioniService.CheckInAsync (e
    /// correggibile a mano quando il check-in viene registrato in ritardo rispetto all'arrivo
    /// vero). Distinto da <see cref="CheckIn"/>, che è la sola data prevista senza orario: i
    /// termini di legge per l'invio delle schedine alla Polizia di Stato — 24 ore dall'arrivo, 6
    /// ore per i soggiorni sotto le 24 ore — si contano da qui (vedi TerminiSchedina). Null sulle
    /// prenotazioni registrate prima di questo campo: in quel caso si ripiega sulla data di
    /// CheckIn a mezzanotte, l'ipotesi più prudente possibile.
    /// </summary>
    public DateTime? CheckInEffettuatoAtUtc { get; set; }

    public int? NumeroOspiti { get; set; }

    /// <summary>
    /// Età all'arrivo dei bambini compresi in <see cref="NumeroOspiti"/>, una per bambino: servono
    /// al supplemento per fascia d'età della tipologia. Si chiede l'età e non la data di nascita,
    /// che al prezzo non serve e arriva comunque con la scheda ospiti al check-in. Vuota = tutti adulti.
    /// </summary>
    public List<int> EtaBambini { get; set; } = [];

    /// <summary>
    /// Trattamento scelto (null = solo pernottamento). I prezzi sotto sono quelli del listino della
    /// struttura al momento della scelta, già in euro: un cambio di listino non deve cambiare il
    /// conto di una prenotazione già fatta. Si ricopiano solo se cambia il trattamento.
    /// </summary>
    public TipoTrattamento? Trattamento { get; set; }

    /// <inheritdoc cref="Trattamento"/>
    public decimal? TrattamentoPrezzoAdulto { get; set; }

    /// <summary>Prezzo a notte di un bambino fino a <see cref="TrattamentoEtaMassimaBambini"/>; null = come un adulto.</summary>
    public decimal? TrattamentoPrezzoBambino { get; set; }

    /// <inheritdoc cref="TrattamentoPrezzoBambino"/>
    public int? TrattamentoEtaMassimaBambini { get; set; }

    /// <summary>
    /// Quello che l'OTA ha mandato oltre ai dati standard: richieste dell'ospite, trattamento, extra
    /// acquistati, informazioni aggiuntive del portale. Riscritto a ogni modifica dall'OTA, mai
    /// modificabile a mano. Può contenere dati particolari (allergie, esigenze di salute): non va nei
    /// log né all'addetto pulizie; i numeri di carta vengono tolti prima di salvarlo.
    /// </summary>
    public string? NoteOta { get; set; }

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

    /// <summary>
    /// Posizione della camera nell'ordine OTA (0 = la prima). Un ordine con più camere diventa una
    /// prenotazione per camera, tutte con lo stesso <see cref="IdPrenotazioneWubook"/>: è quello che le
    /// lega tra loro, e con questo indice fa da chiave di deduplica. 0 per le prenotazioni non OTA.
    /// </summary>
    public int IndiceCameraOta { get; set; }

    /// <summary>
    /// L'ospite ha rinunciato alla pulizia durante il soggiorno. È una sua scelta, non una riduzione
    /// del servizio: la regola della struttura resta quella, e la pagina Pulizie mostra la camera come
    /// "rinunciata" invece di nasconderla. Chi l'ha registrata resta nel log.
    /// </summary>
    public bool RinunciaPulizia { get; set; }

    /// <summary>Come <see cref="RinunciaPulizia"/>, per il cambio biancheria: spesso l'ospite rinuncia al riassetto ma vuole gli asciugamani.</summary>
    public bool RinunciaBiancheria { get; set; }

    /// <summary>Data civile dell'ultima pulizia intermedia fatta. La prossima si conta da qui, non dalla data prevista: una pulizia saltata sposta in avanti le successive.</summary>
    public DateTime? UltimaPuliziaSoggiorno { get; set; }

    /// <inheritdoc cref="UltimaPuliziaSoggiorno"/>
    public DateTime? UltimoCambioBiancheria { get; set; }
}
