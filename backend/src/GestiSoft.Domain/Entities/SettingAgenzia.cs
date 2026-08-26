using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Elenco dei canali/agenzie di provenienza prenotazione (es. "Booking.com", "Diretto"),
/// usato come lista suggerimenti in UI. Porta 1:1 da
/// OrderManagement.Model.BusinesObject.SettingAgenzie del sistema legacy.
/// </summary>
public class SettingAgenzia : TenantEntity
{
    public string Descrizione { get; set; } = string.Empty;
}
