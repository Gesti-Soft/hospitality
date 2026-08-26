using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities.Riferimenti;

/// <summary>
/// Tipo di documento d'identità — tabella di riferimento condivisa (Polizia di Stato
/// "Alloggiati Web"). Porta 1:1 da OrderManagement.Model.BusinesObject.Documenti del sistema legacy.
/// </summary>
public class Documento : Entity
{
    public int TypeId { get; set; }

    public string Codice { get; set; } = string.Empty;

    public string Descrizione { get; set; } = string.Empty;
}
