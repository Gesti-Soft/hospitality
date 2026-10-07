namespace GestiSoft.Domain.Enums;

/// <summary>Cosa è successo a un singolo file XML di una fattura (TrasmissioneFattura).</summary>
public enum EsitoTrasmissioneSdi
{
    /// <summary>File preparato (o scaricato per caricarlo a mano sul portale), mai spedito dal canale.</summary>
    Generato = 1,

    /// <summary>Lo SdI l'ha preso in carico e ha dato un identificativo.</summary>
    Inviato = 2,

    /// <summary>Lo SdI ha rifiutato la chiamata (EI01-EI03): il file non è entrato. Si può ritentare.</summary>
    Rifiutato = 3,

    /// <summary>
    /// La chiamata è caduta senza risposta: il file può essere arrivato o no. Si ritenta con lo
    /// <b>stesso</b> file: se era arrivato, lo SdI scarta il doppione per nome e non nasce una seconda fattura.
    /// </summary>
    EsitoIgnoto = 4,

    Consegnato = 5,

    /// <summary>Impossibilità di recapito: la fattura è valida, sta nel cassetto fiscale del cliente.</summary>
    NonConsegnato = 6,

    Scartato = 7,
}
