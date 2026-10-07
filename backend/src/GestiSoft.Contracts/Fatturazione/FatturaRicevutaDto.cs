namespace GestiSoft.Contracts.Fatturazione;

/// <summary>Una fattura di un fornitore arrivata dallo SdI. Gli importi sono quelli del fornitore, mai ricalcolati.</summary>
public record FatturaRicevutaDto(
    Guid Id,
    /// <summary>La struttura del Cliente a cui si appoggia: le fatture ricevute sono del Cliente.</summary>
    Guid StrutturaId,
    string StrutturaNome,
    string IdentificativoSdi,
    string Fornitore,
    string? FornitorePartitaIva,
    string? FornitoreCodiceFiscale,
    /// <summary>TD01, TD04…: com'è scritto nel file.</summary>
    string TipoDocumento,
    /// <summary>Il numero del fornitore, testo libero.</summary>
    string Numero,
    DateOnly Data,
    string Divisa,
    decimal? ImportoTotale,
    decimal Imponibile,
    decimal Imposta,
    DateTime RicevutaAtUtc,
    /// <summary>Null finché nessuno l'ha aperta: è nuova.</summary>
    DateTime? LettaAtUtc);
