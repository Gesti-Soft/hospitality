using System.Security.Cryptography;
using System.Text;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Notifiche;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione.Sdi;

/// <summary>
/// Le fatture dei fornitori che lo SdI consegna al canale (servizio RicezioneFatture). L'ordine è
/// quello che non perde documenti: consegna già vista? copia su disco, riga con i byte originali,
/// poi lettura e attribuzione. La presa in carico (ER01) per lo SdI è definitiva: detta, la fattura
/// non viene più rimandata. Quindi la si dà solo quando il file è al sicuro, anche se poi la lettura
/// fallisce: il file c'è, il guasto è nostro e si risolve da qui.
/// </summary>
public class SdiRicezioneService(
    ISdiRepository sdi,
    IArchivioSdi archivio,
    IStrutturaRepository strutture,
    NotificaService notifiche,
    ILogEventoService logEventi)
{
    /// <returns>True se si può rispondere ER01 allo SdI; false se il file non è al sicuro e lo SdI deve ritentare.</returns>
    public async Task<bool> RiceviAsync(
        string identificativoSdi, string nomeFile, byte[] file, string? nomeFileMetadati, byte[]? metadati, bool prova, CancellationToken cancellationToken)
    {
        // Consegna ripetuta (lo SdI ritenta se la risposta non gli arriva in tempo): già al sicuro.
        if (await sdi.FileRicevutoEsisteAsync(identificativoSdi, prova, cancellationToken))
        {
            return true;
        }

        try
        {
            await archivio.SalvaAsync(identificativoSdi, prova, nomeFile, file, nomeFileMetadati, metadati, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await logEventi.RegistraAsync(
                LivelloLog.Error,
                $"Fattura ricevuta dallo SdI (identificativo {identificativoSdi}) non salvata su disco: lo SdI la rimanderà.",
                origine: "SdI", dettaglio: ex.Message, categoria: "Fatturazione", cancellationToken: cancellationToken);
            return false;
        }

        var ricevuto = new FileRicevutoSdi
        {
            IdentificativoSdi = identificativoSdi,
            Prova = prova,
            NomeFile = nomeFile,
            File = file,
            NomeFileMetadati = nomeFileMetadati,
            Metadati = metadati,
            Esito = EsitoFileRicevutoSdi.NonLeggibile,
        };
        await sdi.AggiungiFileRicevutoAsync(ricevuto, cancellationToken);

        try
        {
            await ElaboraAsync(ricevuto, cancellationToken);
        }
        catch (Exception ex)
        {
            // Registrato prima, capito poi: il file resta come "non leggibile" e si recupera da qui.
            ricevuto.Esito = EsitoFileRicevutoSdi.NonLeggibile;
            ricevuto.Messaggio = "Errore durante la lettura del file.";
            await sdi.SalvaAsync(cancellationToken);
            await logEventi.RegistraAsync(
                LivelloLog.Error,
                $"Fattura ricevuta dallo SdI (identificativo {identificativoSdi}) conservata ma non elaborata.",
                origine: "SdI", dettaglio: ex.Message, categoria: "Fatturazione", cancellationToken: cancellationToken);
        }

        return true;
    }

    private async Task ElaboraAsync(FileRicevutoSdi ricevuto, CancellationToken cancellationToken)
    {
        var xml = LettoreXmlSdi.EstraiXmlFattura(ricevuto.File);
        var letta = xml is null ? null : LettoreXmlSdi.LeggiFattura(xml);
        if (letta is null || letta.Documenti.Count == 0)
        {
            ricevuto.Messaggio = "Il file non contiene una fattura elettronica leggibile.";
            await sdi.SalvaAsync(cancellationToken);
            await AvvisaAsync(LivelloLog.Warning, $"Fattura ricevuta dallo SdI (identificativo {ricevuto.IdentificativoSdi}) non leggibile: conservata, da controllare.", null, cancellationToken);
            return;
        }

        ricevuto.Fornitore = letta.Fornitore;
        ricevuto.IdentificativoCessionario = letta.CessionarioPartitaIva ?? letta.CessionarioCodiceFiscale;

        // Documenti inventati dallo SdI di collaudo: si conservano e si mostrano, mai tra gli acquisti di un cliente.
        if (ricevuto.Prova)
        {
            ricevuto.Esito = EsitoFileRicevutoSdi.Prova;
            ricevuto.Messaggio = $"Fattura di collaudo da {letta.Fornitore}.";
            await sdi.SalvaAsync(cancellationToken);
            await AvvisaAsync(LivelloLog.Info, $"[COLLAUDO] Fattura ricevuta dallo SdI (identificativo {ricevuto.IdentificativoSdi}).", null, cancellationToken);
            return;
        }

        // A smistare è il documento, non chi chiama: il cessionario. Le fatture ricevute sono del
        // Cliente (scelta dell'utente: in "Fatture ricevute" ogni struttura vede quelle di tutte le
        // strutture del suo Cliente), quindi più strutture dello stesso Cliente con la stessa partita
        // IVA non sono un problema. Nessuna struttura, o strutture di Clienti diversi (un errore di
        // anagrafica): quarantena, perché attribuirla al Cliente sbagliato la mostrerebbe a chi non deve.
        var candidate = await sdi.StrutturePerIdentificativoFiscaleAsync(letta.CessionarioPartitaIva, letta.CessionarioCodiceFiscale, cancellationToken);
        var clienti = candidate.Select(c => c.ClienteId).Distinct().Count();
        if (clienti != 1)
        {
            ricevuto.Esito = clienti == 0 ? EsitoFileRicevutoSdi.SenzaDestinatario : EsitoFileRicevutoSdi.PiuDestinatari;
            ricevuto.Messaggio = clienti == 0
                ? "Nessuna struttura con il servizio SdI abilitato ha la partita IVA o il codice fiscale del destinatario."
                : "Strutture di Clienti diversi hanno la partita IVA o il codice fiscale del destinatario: va attribuita a mano.";
            await sdi.SalvaAsync(cancellationToken);
            await AvvisaAsync(LivelloLog.Warning, $"Fattura ricevuta dallo SdI (identificativo {ricevuto.IdentificativoSdi}) in quarantena: {ricevuto.Messaggio}", null, cancellationToken);
            return;
        }

        await AttribuisciAsync(ricevuto, letta, candidate.Select(c => c.StrutturaId).ToList(), cancellationToken);
    }

    /// <summary>
    /// Registra le fatture del file tra quelle ricevute. <paramref name="strutturaIds"/> sono le
    /// strutture del Cliente con l'identificativo del destinatario: la fattura si appoggia alla prima
    /// e la vedono tutte le strutture del Cliente; l'avviso va a ognuna. Usata anche dall'attribuzione
    /// manuale della quarantena, che non ripete il controllo sul destinatario: è proprio quello già fallito.
    /// </summary>
    public async Task AttribuisciAsync(FileRicevutoSdi ricevuto, LettoreXmlSdi.FatturaLetta letta, IReadOnlyList<Guid> strutturaIds, CancellationToken cancellationToken)
    {
        var strutturaId = strutturaIds[0];
        var nuove = new List<FatturaRicevuta>();
        foreach (var documento in letta.Documenti)
        {
            // L'impronta comprende il numero del documento: senza, la seconda fattura di un file
            // multiplo risulterebbe già importata per colpa della prima.
            var impronta = Impronta(letta.FornitorePartitaIva ?? letta.FornitoreCodiceFiscale ?? letta.Fornitore, documento.Numero, documento.Data, documento.TipoDocumento);
            if (await sdi.FatturaRicevutaEsisteAsync(strutturaIds, impronta, cancellationToken))
            {
                continue;
            }

            nuove.Add(new FatturaRicevuta
            {
                StrutturaId = strutturaId,
                FileRicevutoSdiId = ricevuto.Id,
                IndiceNelFile = documento.Indice,
                IdentificativoSdi = ricevuto.IdentificativoSdi,
                Fornitore = Tronca(letta.Fornitore, 200)!,
                FornitorePartitaIva = Tronca(letta.FornitorePartitaIva, 30),
                FornitoreCodiceFiscale = Tronca(letta.FornitoreCodiceFiscale, 30),
                TipoDocumento = Tronca(documento.TipoDocumento, 10)!,
                Numero = Tronca(documento.Numero, 20)!,
                Data = documento.Data,
                Divisa = Tronca(documento.Divisa, 3)!,
                ImportoTotale = documento.ImportoTotale,
                Imponibile = documento.Imponibile,
                Imposta = documento.Imposta,
                Impronta = impronta,
            });
        }

        ricevuto.StrutturaId = strutturaId;
        ricevuto.Esito = nuove.Count > 0 ? EsitoFileRicevutoSdi.Importata : EsitoFileRicevutoSdi.Duplicata;
        ricevuto.Messaggio = nuove.Count > 0 ? null : "Le fatture del file erano già registrate.";
        await sdi.AggiungiFattureRicevuteAsync(nuove, cancellationToken);
        await sdi.SalvaAsync(cancellationToken);

        if (nuove.Count == 0)
        {
            return;
        }

        await AvvisaAsync(
            LivelloLog.Info,
            $"{(nuove.Count == 1 ? "Ricevuta dallo SdI una fattura" : $"Ricevute dallo SdI {nuove.Count} fatture")} di acquisto (identificativo {ricevuto.IdentificativoSdi}).",
            strutturaId,
            cancellationToken);
        foreach (var destinataria in strutturaIds)
        {
            await notifiche.CreaSeNonEsisteAsync(
                destinataria,
                TipoNotifica.FatturaRicevuta,
                $"sdi:ricevuta:{ricevuto.IdentificativoSdi}",
                nuove.Count == 1 ? "Nuova fattura di acquisto" : $"{nuove.Count} nuove fatture di acquisto",
                "Arrivata dal Sistema di Interscambio: la trovi in Fatturazione, Fatture ricevute.",
                cancellationToken,
                richiedeFinanze: true);
        }
    }

    private async Task AvvisaAsync(LivelloLog livello, string messaggio, Guid? strutturaId, CancellationToken cancellationToken)
    {
        var clienteId = strutturaId is { } id ? (await strutture.GetByIdAsync(id, cancellationToken))?.ClienteId : null;
        await logEventi.RegistraAsync(livello, messaggio, origine: "SdI", clienteId: clienteId, strutturaId: strutturaId, categoria: "Fatturazione", cancellationToken: cancellationToken);
    }

    private static string Impronta(string fornitore, string numero, DateOnly data, string tipo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{FileSdiService.Normalizza(fornitore)}|{numero.Trim().ToUpperInvariant()}|{data:yyyy-MM-dd}|{tipo}")));

    private static string? Tronca(string? testo, int massimo) =>
        testo is null ? null : testo.Length > massimo ? testo[..massimo] : testo;
}
