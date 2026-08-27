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

    /// <summary>
    /// Prezzo valido per un'intera tipologia (livello intermedio tra CameraId specifica e
    /// SettingTipologia.PrezzoDefault) — porta il campo IdTipologia di
    /// OrderManagement.Model.BusinesObject.GestionePrezzi, assente nello schema originale della
    /// Fase 1. Valorizzato in alternativa a CameraId, mai insieme (vedi PrezziCameraService).
    /// </summary>
    public Guid? TipologiaId { get; set; }

    public SettingTipologia? Tipologia { get; set; }

    public DateTime? DataInizio { get; set; }

    public DateTime? DataFine { get; set; }

    public decimal? PrezzoPerNotte { get; set; }
}
