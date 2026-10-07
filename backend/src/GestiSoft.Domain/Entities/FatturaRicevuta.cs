using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Una fattura di un fornitore arrivata dallo SdI. Tabella propria, non righe in più tra le fatture
/// emesse: il numero è del fornitore ed è testo libero, e non c'è uno stato SdI da governare. Un file
/// può contenere più fatture (FatturaElettronicaBody ripetuto): una riga per ognuna. Gli importi
/// sono quelli del fornitore e non si ricalcolano mai: è su quelli che ha liquidato l'IVA.
/// Documento fiscale: si conserva 10 anni (art. 2220 c.c.).
/// <para>
/// È del Cliente, non della sola struttura (scelta dell'utente): StrutturaId è quella a cui si
/// appoggia, ma l'elenco "Fatture ricevute" di ogni struttura mostra quelle di tutte le strutture
/// dello stesso Cliente.
/// </para>
/// </summary>
public class FatturaRicevuta : TenantEntity
{
    public Guid FileRicevutoSdiId { get; set; }

    /// <summary>Posizione della fattura nel file, da 1.</summary>
    public int IndiceNelFile { get; set; } = 1;

    public string IdentificativoSdi { get; set; } = string.Empty;

    /// <summary>Denominazione, oppure nome e cognome, del fornitore.</summary>
    public string Fornitore { get; set; } = string.Empty;

    public string? FornitorePartitaIva { get; set; }

    public string? FornitoreCodiceFiscale { get; set; }

    /// <summary>TD01, TD04…: com'è scritto nel file.</summary>
    public string TipoDocumento { get; set; } = string.Empty;

    /// <summary>Il numero del fornitore, testo libero ("12/A", "2026-0012").</summary>
    public string Numero { get; set; } = string.Empty;

    public DateOnly Data { get; set; }

    public string Divisa { get; set; } = "EUR";

    public decimal? ImportoTotale { get; set; }

    public decimal Imponibile { get; set; }

    public decimal Imposta { get; set; }

    /// <summary>Impronta di file, posizione e numero: la stessa fattura consegnata due volte non si registra due volte.</summary>
    public string Impronta { get; set; } = string.Empty;

    public DateTime RicevutaAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Quando qualcuno della struttura l'ha aperta: le nuove si distinguono dalle già viste.</summary>
    public DateTime? LettaAtUtc { get; set; }
}
