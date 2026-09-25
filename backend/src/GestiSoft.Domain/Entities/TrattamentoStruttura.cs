using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Listino di un trattamento della struttura (colazione, mezza pensione, pensione completa): prezzo
/// a persona e a notte, con un prezzo ridotto facoltativo per i bambini fino a una certa età. Uno
/// per tipo; se non c'è, o non è attivo, nella prenotazione non si può scegliere. La prenotazione
/// copia i prezzi al momento della scelta: un cambio di listino non tocca quelle già fatte.
/// </summary>
public class TrattamentoStruttura : TenantEntity
{
    public TipoTrattamento Tipo { get; set; }

    public bool Attivo { get; set; } = true;

    public decimal PrezzoPerPersona { get; set; }

    /// <summary>Prezzo per i bambini fino a <see cref="EtaMassimaBambini"/> compresa: null = pagano come gli adulti.</summary>
    public decimal? PrezzoBambini { get; set; }

    /// <inheritdoc cref="PrezzoBambini"/>
    public TipoVariazionePrezzo TipoPrezzoBambini { get; set; } = TipoVariazionePrezzo.Euro;

    /// <inheritdoc cref="PrezzoBambini"/>
    public int? EtaMassimaBambini { get; set; }

    /// <summary>
    /// Esercizio convenzionato che serve la colazione, stampato sui buoni (tipico della locazione
    /// breve, dove l'host non può servirla lui). Facoltativo.
    /// </summary>
    public string? EsercizioConvenzionato { get; set; }
}
