namespace GestiSoft.Domain.Enums;

/// <summary>I messaggi che lo SdI manda su una fattura trasmessa.</summary>
public enum TipoRicevutaSdi
{
    /// <summary>Ricevuta di consegna: il cliente l'ha ricevuta.</summary>
    Consegna = 1,

    /// <summary>Impossibilità di recapito: la fattura è valida, sta nel cassetto fiscale del cliente.</summary>
    MancataConsegna = 2,

    /// <summary>Scarto: per lo SdI la fattura non esiste.</summary>
    Scarto = 3,

    /// <summary>Esito del committente: solo fatture verso la PA.</summary>
    Esito = 4,

    /// <summary>Decorrenza termini: solo fatture verso la PA.</summary>
    DecorrenzaTermini = 5,

    /// <summary>Attestazione di trasmissione con impossibilità di recapito: solo fatture verso la PA.</summary>
    AttestazioneTrasmissione = 6,

    /// <summary>Messaggio non riconosciuto: si conserva comunque, e il Log lo segnala.</summary>
    Sconosciuta = 99,
}
