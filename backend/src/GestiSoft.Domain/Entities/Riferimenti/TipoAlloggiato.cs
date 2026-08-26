using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities.Riferimenti;

/// <summary>
/// Tipo di ruolo ospite (es. "Ospite singolo", "Capo famiglia") — tabella di riferimento
/// condivisa (Polizia di Stato "Alloggiati Web"). Porta 1:1 da
/// OrderManagement.Model.BusinesObject.TipoAlloggiato del sistema legacy.
/// </summary>
public class TipoAlloggiato : Entity
{
    public string Codice { get; set; } = string.Empty;

    public string Descrizione { get; set; } = string.Empty;
}
