using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities.Riferimenti;

/// <summary>
/// Comune italiano — tabella di riferimento condivisa (Polizia di Stato "Alloggiati Web").
/// Porta 1:1 da OrderManagement.Model.BusinesObject.Comuni del sistema legacy.
/// </summary>
public class Comune : Entity
{
    public long Codice { get; set; }

    public string Descrizione { get; set; } = string.Empty;

    public string? Provincia { get; set; }

    public string? CodiceBelfiore { get; set; }

    public string? Cap { get; set; }
}
