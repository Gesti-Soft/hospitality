using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Traccia ogni evento di prenotazione Wubook intercettato per una Struttura (coppia Lcode/Rcode),
/// a prescindere dall'esito dell'importazione locale — a differenza dello storico "letto/non letto"
/// di gestisoft.it (pensato solo per sapere cosa manca ancora da elaborare, non per un recupero a
/// posteriori), questa riga resta per sempre: se un'importazione fallisce o produce dati sospetti,
/// Lcode+Rcode bastano per recuperare a mano la prenotazione originale da Wubook (fetch_booking).
/// Una riga per Rcode per Struttura, aggiornata (non duplicata) ad ogni nuovo tentativo dello stesso
/// evento.
/// </summary>
public class WubookEventoRicevuto : TenantEntity
{
    public string Lcode { get; set; } = string.Empty;

    public int Rcode { get; set; }

    public bool ImportazioneRiuscita { get; set; }

    /// <summary>Motivo del fallimento (fetch da Wubook o importazione locale) — null se ImportazioneRiuscita è true.</summary>
    public string? MessaggioErrore { get; set; }
}
