using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities.Riferimenti;

/// <summary>
/// Stato estero — tabella di riferimento condivisa (Polizia di Stato "Alloggiati Web").
/// Porta 1:1 da OrderManagement.Model.BusinesObject.Stati del sistema legacy (il campo legacy
/// "Provincia" in realtà conteneva il nome inglese dello stato per un bug di copia-incolla da
/// Comuni: qui si chiama correttamente NomeInglese).
/// </summary>
public class Stato : Entity
{
    public long Codice { get; set; }

    public string Descrizione { get; set; } = string.Empty;

    public string? NomeInglese { get; set; }

    /// <summary>Codice ISO2 (es. "AF").</summary>
    public string? Acronimo { get; set; }
}
