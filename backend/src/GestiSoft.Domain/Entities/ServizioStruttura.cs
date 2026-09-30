using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Servizio extra che la struttura vende oltre al soggiorno (escursione, parcheggio, transfer…),
/// creato dall'utente. A differenza dei trattamenti una prenotazione può averne più d'uno. Eliminarlo
/// lo nasconde soltanto: le prenotazioni che lo hanno venduto lo tengono, con nome e prezzo copiati.
/// </summary>
public class ServizioStruttura : TenantEntity
{
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Identificativo stabile (es. "SPA"), lo stesso del servizio sul sito web della struttura: le
    /// prenotazioni del sito arrivano da WuBook con i servizi indicati per codice (vedi
    /// ServiziSitoWeb), e da qui si sa di quale servizio si tratta. Facoltativo, univoco per struttura.
    /// </summary>
    public string? Codice { get; set; }

    public decimal Prezzo { get; set; }

    public ModalitaPrezzoServizio Modalita { get; set; } = ModalitaPrezzoServizio.APersona;

    /// <summary>Offerto: se no, non si può aggiungere a una prenotazione nuova.</summary>
    public bool Attivo { get; set; } = true;

    public bool Eliminato { get; set; }

    /// <summary>
    /// Aliquota con cui si fattura (la SPA può essere al 22%, un parcheggio per soli ospiti al 10%
    /// come l'alloggio: dipende da com'è offerto, lo decide il commercialista). Entrambi null =
    /// quella predefinita della struttura. Mai tutti e due.
    /// </summary>
    public AliquotaIva? AliquotaIva { get; set; }

    /// <inheritdoc cref="AliquotaIva"/>
    public NaturaIva? Natura { get; set; }
}
