using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>Porta 1:1 da OrderManagement.Model.BusinesObject.Spese del sistema legacy.</summary>
public class Spesa : TenantEntity
{
    public string? TipoSpesa { get; set; }

    public string? Nome { get; set; }

    public decimal ImportoSpesa { get; set; }

    public string? Descrizione { get; set; }

    public string? MetodoPagamento { get; set; }

    public DateTime? DataSpesa { get; set; }

    public int? Anno { get; set; }
}
