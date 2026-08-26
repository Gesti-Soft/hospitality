using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Riga di calendario prezzi per una camera in un intervallo di date. Porta 1:1 da
/// OrderManagement.Model.BusinesObject.GestionePrezzi del sistema legacy.
/// </summary>
public class GestionePrezzo : TenantEntity
{
    public Guid? CameraId { get; set; }

    public SettingRoom? Camera { get; set; }

    public DateTime? DataInizio { get; set; }

    public DateTime? DataFine { get; set; }

    public decimal? PrezzoPerNotte { get; set; }
}
