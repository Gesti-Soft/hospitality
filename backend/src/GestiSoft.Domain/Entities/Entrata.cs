using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Porta 1:1 da OrderManagement.Model.BusinesObject.Entrate del sistema legacy (il campo data era
/// erroneamente chiamato "DataSpesa" per copia-incolla da Spese: qui è "Data").
/// </summary>
public class Entrata : TenantEntity
{
    public string? TipoEntrata { get; set; }

    public string? Nome { get; set; }

    public decimal ImportoEntrata { get; set; }

    public string? Descrizione { get; set; }

    public DateTime? Data { get; set; }

    public int? Anno { get; set; } = DateTime.UtcNow.Year;
}
