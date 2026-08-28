using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Soggiorno minimo/massimo per una camera valido solo in un periodo specifico (es. minimo 7
/// notti ad agosto), in aggiunta al <see cref="SettingRoom.SoggiornoMinimo"/> fisso — porta 1:1
/// <c>OtaService.Web.Controllers.RoomsController</c> (stay restrictions) e la tabella legacy
/// <c>RoomStayRestriction</c>. Pushato su Wubook con lo stesso metodo
/// (<c>rplan_update_rplan_values</c>, piano di default pid=0) già usato per il soggiorno minimo
/// fisso — vedi WubookDisponibilitaService, che per ogni giorno preferisce una regola per il
/// periodo attiva a quella fissa quando presente.
/// </summary>
public class RestrizioneSoggiornoCamera : TenantEntity
{
    public Guid CameraId { get; set; }

    public SettingRoom? Camera { get; set; }

    public DateTime DataInizio { get; set; }

    public DateTime DataFine { get; set; }

    public int? MinStay { get; set; }

    public int? MaxStay { get; set; }

    public string? Motivo { get; set; }
}
