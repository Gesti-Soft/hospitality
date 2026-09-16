using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione;

/// <summary>
/// Genera i documenti di una fattura al volo, su richiesta — mai persistiti su disco/storage del
/// server (scelta dell'utente: si scaricano sul PC locale ogni volta, zero spazio occupato sul
/// server, coerente col fatto che il legacy non inviava comunque nulla a SDI). Il PDF è nuovo
/// (non esiste nel legacy); l'XML riprende la struttura FatturaElettronica ordinaria (FPR12,
/// verso privati) generata dal client WPF legacy con la libreria NuGet "FatturaElettronica",
/// riscritta qui direttamente con System.Xml per non dipendere da quel pacchetto (di cui non è
/// stata verificata la compatibilità/manutenzione su .NET 10).
/// </summary>
public interface IFatturaDocumentGenerator
{
    byte[] GeneraPdf(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda);

    byte[] GeneraXmlSdi(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda);

    /// <summary>
    /// Motivi per cui l'XML non è trasmissibile, vuoto se è a posto — stesso principio di
    /// SchedinaAlloggiatiWebBuilder.Valida: si controlla ciò che <see cref="GeneraXmlSdi"/> andrebbe
    /// a scrivere, perché un campo obbligatorio mancante diventa un elemento vuoto e lo SDI scarta
    /// il file senza che l'operatore possa capire quale dato mancava. Meglio non produrlo affatto e
    /// dirlo subito, quando la fattura è ancora sotto gli occhi di chi l'ha fatta.
    /// </summary>
    IReadOnlyList<string> ValidaPerSdi(DatiFattura fattura, DatiCliente? cliente, DatiAziendali? azienda);
}
