using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione.Sdi;

/// <summary>Ricevute e consegne del canale SdI: tabelle senza contesto di struttura, si cercano per nome file o identificativo.</summary>
public interface ISdiRepository
{
    Task<TrasmissioneFattura?> TrasmissionePerNomeFileAsync(string nomeFile, CancellationToken cancellationToken);

    Task<TrasmissioneFattura?> TrasmissionePerIdentificativoAsync(string identificativoSdi, CancellationToken cancellationToken);

    Task AggiungiRicevutaAsync(RicevutaSdi ricevuta, CancellationToken cancellationToken);

    Task<bool> FileRicevutoEsisteAsync(string identificativoSdi, bool prova, CancellationToken cancellationToken);

    Task AggiungiFileRicevutoAsync(FileRicevutoSdi file, CancellationToken cancellationToken);

    /// <summary>La stessa fattura è già registrata su una di queste strutture (tutte dello stesso Cliente)?</summary>
    Task<bool> FatturaRicevutaEsisteAsync(IReadOnlyCollection<Guid> strutturaIds, string impronta, CancellationToken cancellationToken);

    Task AggiungiFattureRicevuteAsync(IReadOnlyList<FatturaRicevuta> fatture, CancellationToken cancellationToken);

    /// <summary>
    /// Strutture attive, con il servizio SdI concesso dal Super Admin, i cui dati fiscali hanno questa
    /// partita IVA o questo codice fiscale, normalizzati anche dal lato del database: in anagrafica sono
    /// scritti a mano, con "IT", punti, spazi. Senza concessione la fattura resta in quarantena.
    /// </summary>
    Task<IReadOnlyList<StrutturaDestinataria>> StrutturePerIdentificativoFiscaleAsync(string? partitaIva, string? codiceFiscale, CancellationToken cancellationToken);

    /// <summary>Salva le modifiche alle entità già caricate (trasmissione, fattura, file ricevuto).</summary>
    Task SalvaAsync(CancellationToken cancellationToken);
}

/// <summary>Una struttura con l'identificativo fiscale del destinatario, e il Cliente a cui appartiene.</summary>
public record StrutturaDestinataria(Guid StrutturaId, Guid ClienteId);

/// <summary>
/// La copia su disco delle consegne ricevute, fuori dal database: dal database l'applicazione serve i
/// file, dal disco si ripristina con un tar anche se l'applicazione non parte.
/// </summary>
public interface IArchivioSdi
{
    /// <summary>Scrive in modo atomico (file temporaneo, flush, rinomina): un file mezzo scritto sembrerebbe a posto.</summary>
    /// <exception cref="IOException">La copia non è riuscita: allora allo SdI non si conferma la presa in carico.</exception>
    Task SalvaAsync(string identificativoSdi, bool prova, string nomeFile, byte[] file, string? nomeFileMetadati, byte[]? metadati, CancellationToken cancellationToken);
}
