using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione;

/// <summary>
/// Identità del canale SdI di GestiSoft, dalla configurazione (sezione "Sdi"): è del canale, come i
/// certificati, non di una struttura.
/// </summary>
public interface IConfigurazioneCanaleSdi
{
    /// <summary>
    /// Codice fiscale di chi spedisce i file dal canale: va in IdTrasmittente e nel nome del file.
    /// Non è la partita IVA scritta nel certificato: per una ditta individuale i due numeri sono
    /// diversi, e la P.IVA in IdTrasmittente fa scartare il file (errore 00300). Null finché il canale
    /// non è configurato: allora nessuna struttura può spedire, e non si ripiega sul suo codice.
    /// </summary>
    string? CodiceFiscaleTrasmittente { get; }

    /// <summary>
    /// Primo progressivo di un codice fiscale mai visto prima. Alto apposta: i numeri già spesi da
    /// quel codice fiscale fuori dal gestionale non si conoscono, e un buco non costa niente mentre un
    /// numero riusato è uno scarto.
    /// </summary>
    int ProgressivoIniziale { get; }

    /// <summary>Codice destinatario del canale in ricezione, assegnato dallo SdI. Null finché non c'è.</summary>
    string? CodiceDestinatario { get; }

    /// <summary>True se si spedisce allo SdI di collaudo: le fatture inviate da lì non contano.</summary>
    bool Collaudo { get; }
}

/// <summary>I dati di DatiTrasmissione che dipendono da chi spedisce il file, non dalla fattura.</summary>
public record IntestazioneTrasmissione(string IdPaese, string IdCodice, string ProgressivoInvio);

public interface ITrasmissioneFatturaRepository
{
    /// <summary>L'ultimo file generato per la fattura, collaudo compreso.</summary>
    Task<TrasmissioneFattura?> GetUltimaAsync(Guid fatturaId, CancellationToken cancellationToken);

    /// <summary>Tutti i file della fattura, dal più recente, senza i byte dell'XML.</summary>
    Task<IReadOnlyList<TrasmissioneFattura>> ListByFatturaAsync(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken);

    Task<TrasmissioneFattura?> GetAsync(Guid strutturaId, Guid trasmissioneId, CancellationToken cancellationToken);

    /// <summary>Fra queste, le fatture di cui qualcuno ha scaricato l'XML per caricarlo da sé sul portale.</summary>
    Task<IReadOnlySet<Guid>> FattureConXmlScaricatoAsync(Guid strutturaId, IReadOnlyCollection<Guid> fatturaIds, CancellationToken cancellationToken);

    Task AddAsync(TrasmissioneFattura trasmissione, CancellationToken cancellationToken);

    /// <summary>Salva le modifiche alla trasmissione e alla fattura caricate nello stesso contesto.</summary>
    Task SalvaAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Prende il progressivo successivo per il codice fiscale, in modo atomico (un solo UPDATE, che
    /// tiene il lock sulla riga): due fatture spedite insieme non prendono mai lo stesso numero. Se il
    /// codice fiscale non ha ancora un contatore, parte da <paramref name="valoreIniziale"/>.
    /// </summary>
    Task<int> ProssimoProgressivoInvioAsync(string codiceFiscale, int valoreIniziale, CancellationToken cancellationToken);
}

/// <summary>La risposta di RiceviFile: identificativo e data se il file è entrato, altrimenti il codice EI0x.</summary>
public record RispostaRiceviFile(string? IdentificativoSdi, DateTimeOffset? DataOraRicezione, string? Errore)
{
    public bool Accettato => Errore is null && IdentificativoSdi is not null;
}

/// <summary>La spedizione di un file allo SdI (servizio RiceviFile, con il certificato del canale).</summary>
public interface ISdiTrasmissioneClient
{
    /// <summary>
    /// True se l'endpoint è quello di collaudo: il file parte davvero, ma verso uno SdI dove non ha
    /// valore, quindi la fattura non deve risultare inviata.
    /// </summary>
    bool AmbienteDiProva { get; }

    /// <exception cref="SdiNonRaggiungibileException">La chiamata è caduta: non si sa se il file sia arrivato.</exception>
    Task<RispostaRiceviFile> InviaAsync(string nomeFile, byte[] contenuto, CancellationToken cancellationToken);
}

/// <summary>Nessuna risposta dallo SdI: l'esito è ignoto, il file può essere arrivato.</summary>
public class SdiNonRaggiungibileException(string message, Exception? inner = null) : Exception(message, inner);
