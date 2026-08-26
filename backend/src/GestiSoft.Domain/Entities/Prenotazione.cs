using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Una prenotazione/soggiorno. Porta 1:1 da OrderManagement.Model.BusinesObject.Agenzie del
/// sistema legacy — rinominata da "Agenzie" a "Prenotazione" perché il nome legacy era fuorviante
/// (l'entità è la prenotazione, "Agenzia" è solo uno dei suoi campi: il canale di provenienza).
/// </summary>
public class Prenotazione : TenantEntity
{
    public Guid? CameraId { get; set; }

    public SettingRoom? Camera { get; set; }

    /// <summary>Canale/agenzia di provenienza (es. "Booking.com", "Diretto") — testo libero, vedi SettingAgenzia.</summary>
    public string? Agenzia { get; set; }

    public string? NumeroPrenotazione { get; set; }

    public decimal? ImportoPrenotazione { get; set; }

    public decimal? ImportoPagato { get; set; }

    public decimal? ImportoTotale { get; set; }

    public DateTime? CheckIn { get; set; }

    public DateTime? CheckOut { get; set; }

    public int? NumeroOspiti { get; set; }

    public bool StatePolice { get; set; }

    public bool PMS { get; set; }

    public bool PayTourist { get; set; }

    public string? CheckEditSelection { get; set; }

    public string? Supply { get; set; }

    public int Anno { get; set; } = DateTime.UtcNow.Year;

    public decimal? TotalTax { get; set; }

    public StatoPrenotazione? StatoPrenotazione { get; set; }
}
