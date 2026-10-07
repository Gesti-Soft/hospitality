namespace GestiSoft.Domain.Enums;

/// <summary>Cosa ne è stato di un file ricevuto dallo SdI (FileRicevutoSdi).</summary>
public enum EsitoFileRicevutoSdi
{
    /// <summary>Letto e registrato tra le fatture ricevute della struttura.</summary>
    Importata = 1,

    /// <summary>Le fatture che contiene erano già registrate.</summary>
    Duplicata = 2,

    /// <summary>Nessuna struttura ha la partita IVA o il codice fiscale del cessionario: in quarantena.</summary>
    SenzaDestinatario = 3,

    /// <summary>
    /// Strutture di Clienti diversi hanno lo stesso identificativo fiscale: è un errore di anagrafica.
    /// In quarantena, perché attribuirla al Cliente sbagliato la mostrerebbe a chi non deve. Più
    /// strutture dello stesso Cliente invece vanno bene: le fatture ricevute sono del Cliente.
    /// </summary>
    PiuDestinatari = 4,

    /// <summary>Il file non si riesce a leggere. È conservato: si recupera da qui.</summary>
    NonLeggibile = 5,

    /// <summary>Arrivato dallo SdI di collaudo: documento inventato, si mostra e basta.</summary>
    Prova = 6,
}
