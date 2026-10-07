using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Copia di un documento fiscale di una struttura eliminata definitivamente: fatture emesse, note di
/// credito, ricevute di locazione breve, fatture ricevute. La conservazione a norma la fa l'Agenzia
/// delle Entrate, se il Cliente vi ha aderito; questa serve a restituirgli i documenti se li chiede,
/// e copre le ricevute di locazione breve, che dallo SdI non passano (scelta dell'utente).
/// <para>
/// Slegata dalla struttura, che non esiste più: nome e partita IVA sono copiati. La vede solo il
/// Super Admin. Contiene dati personali (ospiti, fornitori ditte individuali): si cancella da sola
/// alla <see cref="Scadenza"/>, il 31 dicembre del decimo anno dalla data del documento (art. 2220 c.c.).
/// </para>
/// </summary>
public class DocumentoFiscaleArchiviato : Entity
{
    public Guid ClienteId { get; set; }

    public string ClienteRagioneSociale { get; set; } = string.Empty;

    /// <summary>L'Id della struttura eliminata: non è una chiave, la riga non esiste più.</summary>
    public Guid StrutturaIdOriginale { get; set; }

    public string StrutturaNome { get; set; } = string.Empty;

    public string? StrutturaPartitaIva { get; set; }

    public TipoDocumentoArchiviato Tipo { get; set; }

    /// <summary>"12/2026" per i documenti emessi, il numero del fornitore per quelli ricevuti.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateOnly Data { get; set; }

    /// <summary>Il cliente intestatario per i documenti emessi, il fornitore per quelli ricevuti.</summary>
    public string? Controparte { get; set; }

    public decimal? Importo { get; set; }

    /// <summary>Il PDF del documento emesso, generato al momento dell'archiviazione. Null per le fatture ricevute.</summary>
    public byte[]? Pdf { get; set; }

    /// <summary>Il file elettronico: l'XML partito per lo SdI, o il file ricevuto dal fornitore. Null se non c'è mai stato.</summary>
    public byte[]? File { get; set; }

    public string? NomeFile { get; set; }

    public DateTime ArchiviatoAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Dal giorno dopo si cancella (job del Worker).</summary>
    public DateOnly Scadenza { get; set; }

    /// <summary>31 dicembre del decimo anno dalla data del documento.</summary>
    public static DateOnly ScadenzaPer(DateOnly data) => new(data.Year + 10, 12, 31);
}
