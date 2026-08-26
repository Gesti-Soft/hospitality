using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>Porta 1:1 da OrderManagement.Model.BusinesObject.Cauzioni del sistema legacy.</summary>
public class Cauzione : TenantEntity
{
    public Guid PrenotazioneId { get; set; }

    public Prenotazione? Prenotazione { get; set; }

    public decimal? ImportoCauzione { get; set; }

    public DateTime? DataInserimento { get; set; }
}
