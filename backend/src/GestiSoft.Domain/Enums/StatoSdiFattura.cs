namespace GestiSoft.Domain.Enums;

/// <summary>
/// A che punto è una fattura con il Sistema di Interscambio. Riassume l'ultima trasmissione: lo
/// storico di ogni file sta in TrasmissioneFattura. Null sulle ricevute di locazione breve (non
/// passano dallo SdI) e sulle fatture emesse prima del canale, che non si possono spedire da qui:
/// qualcuna può essere già stata caricata a mano sul portale, e rispedirla farebbe un doppione.
/// </summary>
public enum StatoSdiFattura
{
    /// <summary>Emessa e non ancora spedita dal canale. Si può modificare.</summary>
    DaInviare = 1,

    /// <summary>Presa in carico dallo SdI (ha l'identificativo), in attesa della ricevuta. Bloccata.</summary>
    Inviata = 2,

    /// <summary>Ricevuta di consegna: il cliente l'ha ricevuta. Bloccata, si corregge solo con una nota di credito.</summary>
    Consegnata = 3,

    /// <summary>
    /// Impossibilità di recapito: la fattura è <b>valida</b> e sta nel cassetto fiscale del cliente.
    /// Basta consegnargli la copia di cortesia. Bloccata come una consegnata.
    /// </summary>
    NonConsegnata = 4,

    /// <summary>Ricevuta di scarto: per lo SdI non è mai esistita. Si corregge e si rispedisce con lo stesso numero.</summary>
    Scartata = 5,
}
