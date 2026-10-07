using System.IO.Compression;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Clienti;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Pulizie;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione;

/// <summary>
/// Archivio dei documenti fiscali delle strutture eliminate definitivamente (scelta dell'utente): le
/// copie si preparano prima dell'eliminazione e si salvano nella stessa transazione. Le vede solo il
/// Super Admin, per restituirle al Cliente; ogni lettura resta nel Log. Si cancellano da sole alla
/// scadenza dei 10 anni (PuliziaArchivioDocumentiJob).
/// </summary>
public class ArchivioDocumentiService(
    IArchivioDocumentiRepository archivio,
    FatturazioneService fatturazione,
    IFatturaRicevutaRepository fattureRicevute,
    IClienteRepository clienti,
    ILogEventoService logEventi)
{
    /// <summary>Le copie di tutti i documenti fiscali della struttura: emessi (PDF ed eventuale XML) e ricevuti (file originale).</summary>
    public async Task<IReadOnlyList<DocumentoFiscaleArchiviato>> PreparaAsync(Struttura struttura, CancellationToken cancellationToken)
    {
        var ragioneSociale = (await clienti.GetByIdAsync(struttura.ClienteId, cancellationToken))?.RagioneSociale ?? string.Empty;
        var documenti = (await fatturazione.DocumentiEmessiPerArchivioAsync(struttura, ragioneSociale, cancellationToken)).ToList();

        foreach (var ricevuta in await fattureRicevute.ListAsync([struttura.Id], anno: null, cancellationToken))
        {
            var file = await fattureRicevute.GetFileAsync(ricevuta.FileRicevutoSdiId, cancellationToken);
            documenti.Add(new DocumentoFiscaleArchiviato
            {
                ClienteId = struttura.ClienteId,
                ClienteRagioneSociale = ragioneSociale,
                StrutturaIdOriginale = struttura.Id,
                StrutturaNome = struttura.Nome,
                StrutturaPartitaIva = documenti.FirstOrDefault()?.StrutturaPartitaIva,
                Tipo = TipoDocumentoArchiviato.FatturaRicevuta,
                Numero = ricevuta.Numero,
                Data = ricevuta.Data,
                Controparte = ricevuta.Fornitore,
                Importo = ricevuta.ImportoTotale ?? ricevuta.Imponibile + ricevuta.Imposta,
                File = file?.File,
                NomeFile = file?.NomeFile,
                Scadenza = DocumentoFiscaleArchiviato.ScadenzaPer(ricevuta.Data),
            });
        }

        return documenti;
    }

    public async Task<IReadOnlyList<DocumentoFiscaleArchiviato>> ListaAsync(ICurrentUser currentUser, Guid? clienteId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await archivio.ListAsync(clienteId, cancellationToken);
    }

    /// <param name="pdf">True il PDF, false il file elettronico (XML o file del fornitore).</param>
    public async Task<(string NomeFile, byte[] Contenuto)> ScaricaAsync(ICurrentUser currentUser, Guid id, bool pdf, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        var documento = await archivio.GetAsync(id, cancellationToken) ?? throw new NotFoundException("Documento non trovato.");
        var contenuto = (pdf ? documento.Pdf : documento.File) ?? throw new NotFoundException("Il documento non ha questo file.");
        var nome = pdf ? $"{NomeBase(documento)}.pdf" : documento.NomeFile ?? $"{NomeBase(documento)}.xml";
        await RegistraLetturaAsync(currentUser, documento.ClienteId, $"Scaricato dall'archivio un documento fiscale ({nome}).", cancellationToken);
        return (nome, contenuto);
    }

    /// <summary>Tutti i documenti di un Cliente in uno zip, una cartella per struttura: per restituirglieli.</summary>
    public async Task<(string NomeFile, byte[] Contenuto)> ZipClienteAsync(ICurrentUser currentUser, Guid clienteId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        var documenti = await archivio.ListCompletiAsync(clienteId, cancellationToken);
        if (documenti.Count == 0)
        {
            throw new NotFoundException("Nessun documento archiviato per questo Cliente.");
        }

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usati = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in documenti)
            {
                var cartella = $"{SoloCaratteriSicuri(d.StrutturaNome)}-{d.StrutturaIdOriginale.ToString()[..8]}";
                if (d.Pdf is { } pdf)
                {
                    await AggiungiAsync(zip, usati, $"{cartella}/{NomeBase(d)}.pdf", pdf, cancellationToken);
                }

                if (d.File is { } file)
                {
                    await AggiungiAsync(zip, usati, $"{cartella}/{SoloCaratteriSicuri(d.NomeFile ?? $"{NomeBase(d)}.xml")}", file, cancellationToken);
                }
            }
        }

        await RegistraLetturaAsync(currentUser, clienteId, $"Scaricati dall'archivio {documenti.Count} documenti fiscali del Cliente.", cancellationToken);
        return ($"documenti-archiviati-{SoloCaratteriSicuri(documenti[0].ClienteRagioneSociale)}.zip", memoria.ToArray());
    }

    /// <summary>Per il Worker: cancella i documenti oltre la scadenza dei 10 anni.</summary>
    public async Task<int> EliminaScadutiAsync(CancellationToken cancellationToken)
    {
        var eliminati = await archivio.EliminaScadutiAsync(DateOnly.FromDateTime(PulizieSoggiornoService.Oggi()), cancellationToken);
        if (eliminati > 0)
        {
            await logEventi.RegistraAsync(
                LivelloLog.Info, $"Archivio documenti fiscali: {eliminati} documenti cancellati alla scadenza dei 10 anni.",
                origine: "Worker", categoria: "SuperAdmin", cancellationToken: cancellationToken);
        }

        return eliminati;
    }

    private static async Task AggiungiAsync(ZipArchive zip, HashSet<string> usati, string percorso, byte[] contenuto, CancellationToken cancellationToken)
    {
        // Due documenti con lo stesso nome (stesso numero di fornitori diversi): il secondo prende un suffisso.
        var nome = percorso;
        for (var i = 2; !usati.Add(nome); i++)
        {
            nome = $"{Path.ChangeExtension(percorso, null)}-{i}{Path.GetExtension(percorso)}";
        }

        await using var voce = zip.CreateEntry(nome, CompressionLevel.Optimal).Open();
        await voce.WriteAsync(contenuto, cancellationToken);
    }

    private static string NomeBase(DocumentoFiscaleArchiviato d)
    {
        var tipo = d.Tipo switch
        {
            TipoDocumentoArchiviato.NotaDiCredito => "nota-di-credito",
            TipoDocumentoArchiviato.RicevutaLocazioneBreve => "ricevuta",
            TipoDocumentoArchiviato.FatturaRicevuta => "fattura-ricevuta",
            _ => "fattura",
        };
        return $"{d.Data:yyyy-MM-dd}-{tipo}-{SoloCaratteriSicuri(d.Numero)}";
    }

    private static string SoloCaratteriSicuri(string testo)
    {
        var pulito = new string(testo.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-').ToArray()).Trim('-');
        return pulito.Length == 0 ? "documento" : pulito;
    }

    /// <summary>Chi ha letto i documenti di un Cliente deve restare ricostruibile: contengono dati personali.</summary>
    private Task RegistraLetturaAsync(ICurrentUser currentUser, Guid clienteId, string messaggio, CancellationToken cancellationToken) =>
        logEventi.RegistraAsync(LivelloLog.Info, messaggio, origine: "SuperAdmin", clienteId: clienteId, categoria: "SuperAdmin", operatore: currentUser.Email, cancellationToken: cancellationToken);

    private static void RichiediSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può accedere all'archivio dei documenti.");
        }
    }
}
