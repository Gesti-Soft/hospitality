using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Chiusura manuale di una camera per un periodo (es. manutenzione), indipendente dalle
/// prenotazioni — porta 1:1 <c>OtaService.Web.Controllers.RoomsController</c> (closures) e la
/// tabella legacy <c>StatusRoom</c>. <see cref="Quantita"/> è portato per fedeltà di schema (nel
/// legacy riduceva la disponibilità di una "camera virtuale" che raggruppava più unità fisiche),
/// ma nell'attuale sincronizzazione Wubook (per singola camera, non ancora per Tipologia — vedi
/// nota "Da fare" nel piano) una chiusura attiva azzera semplicemente la disponibilità della
/// camera per il periodo, a prescindere dal valore: la quantità tornerà rilevante quando la
/// sincronizzazione sarà per Tipologia.
/// </summary>
public class ChiusuraCamera : TenantEntity
{
    public Guid CameraId { get; set; }

    public SettingRoom? Camera { get; set; }

    public DateTime DataInizio { get; set; }

    public DateTime DataFine { get; set; }

    public string? Motivo { get; set; }

    public int? Quantita { get; set; }
}
