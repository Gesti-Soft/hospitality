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
}
