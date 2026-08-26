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
}
