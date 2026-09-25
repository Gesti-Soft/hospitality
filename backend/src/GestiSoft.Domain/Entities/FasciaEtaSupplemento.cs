using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Supplemento ridotto per i bambini ospitati oltre gli ospiti inclusi nel prezzo della tipologia.
/// Stesso modello di Booking: fino a tre fasce da 0 a 17 anni, età comprese, ognuna con un importo
/// a notte in euro o in percentuale del supplemento pieno (0 = gratis). Chi ha 18 anni o più, o un'età fuori da ogni fascia, paga il
/// supplemento pieno della tipologia (<see cref="SettingTipologia.Implemento"/>).
/// </summary>
public class FasciaEtaSupplemento : TenantEntity
{
    public Guid TipologiaId { get; set; }

    public int EtaMin { get; set; }

    public int EtaMax { get; set; }

    public decimal ImportoPerNotte { get; set; }

    /// <summary>Euro a notte, oppure percentuale del supplemento pieno della tipologia (es. 50% = metà di quello di un adulto).</summary>
    public TipoVariazionePrezzo TipoImporto { get; set; } = TipoVariazionePrezzo.Euro;
}
