namespace GestiSoft.Domain.Enums;

/// <summary>Che documento fiscale è stato archiviato all'eliminazione della sua struttura.</summary>
public enum TipoDocumentoArchiviato
{
    FatturaEmessa = 1,
    NotaDiCredito = 2,
    /// <summary>Ricevuta di locazione breve: non passa dallo SdI, la conservazione dell'Agenzia non la copre.</summary>
    RicevutaLocazioneBreve = 3,
    /// <summary>Fattura di un fornitore arrivata dallo SdI.</summary>
    FatturaRicevuta = 4,
}
