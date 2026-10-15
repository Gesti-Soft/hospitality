using GestiSoft.Application.Auth;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Notifiche;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione.Sdi;

/// <summary>
/// Le ricevute dello SdI sulle fatture spedite dal canale (servizio TrasmissioneFatture). Ogni
/// ricevuta si conserva com'è arrivata, poi si aggancia al file per nome (il dato preciso) o, se non
/// basta, per identificativo, e aggiorna file e fattura. Chi chiama non riceve mai un errore: per lo
/// SdI un errore vuol dire "riprova", e ritenterebbe la stessa ricevuta all'infinito.
/// </summary>
public class SdiRicevuteService(
    ISdiRepository sdi,
    IDatiFatturaRepository fatture,
    IStrutturaRepository strutture,
    NotificaService notifiche,
    ILogEventoService logEventi,
    IConfigurazioneCanaleSdi canale)
{
    /// <param name="nomeFileBusta">Il NomeFile della chiamata SOAP (il file della ricevuta).</param>
    /// <param name="prova">Arrivata sull'endpoint di collaudo: si conserva e si mostra, non si applica.</param>
    public async Task ImportaAsync(string? nomeFileBusta, string? identificativoBusta, byte[] file, bool prova, CancellationToken cancellationToken)
    {
        var letta = LettoreXmlSdi.LeggiRicevuta(file);
        var identificativo = letta?.IdentificativoSdi ?? identificativoBusta;

        var trasmissione = letta?.NomeFile is { } nomeFattura ? await sdi.TrasmissionePerNomeFileAsync(nomeFattura, cancellationToken) : null;
        if (trasmissione is null && identificativo is not null)
        {
            trasmissione = await sdi.TrasmissionePerIdentificativoAsync(identificativo, cancellationToken);
        }

        var ricevuta = new RicevutaSdi
        {
            StrutturaId = trasmissione?.StrutturaId,
            TrasmissioneFatturaId = trasmissione?.Id,
            Tipo = letta?.Tipo ?? TipoRicevutaSdi.Sconosciuta,
            IdentificativoSdi = identificativo,
            NomeFileFattura = letta?.NomeFile,
            NomeFile = nomeFileBusta,
            Xml = file,
            Prova = prova,
            Descrizione = letta?.Testo,
        };
        await sdi.AggiungiRicevutaAsync(ricevuta, cancellationToken);

        if (letta is null || trasmissione is null)
        {
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                letta is null
                    ? $"{(prova ? "[COLLAUDO] " : "")}Ricevuta SdI non leggibile ({nomeFileBusta}): conservata, da controllare."
                    : $"{(prova ? "[COLLAUDO] " : "")}Ricevuta SdI {Nome(letta.Tipo)} per il file {letta.NomeFile}, che non corrisponde a nessuna fattura: conservata.",
                origine: "SdI",
                categoria: "Fatturazione",
                cancellationToken: cancellationToken);
            return;
        }

        await ApplicaAsync(trasmissione, letta, prova, cancellationToken);
    }

    /// <summary>
    /// File: il suo esito, sempre (anche in collaudo, per vedere il giro completo). Fattura: solo se la
    /// ricevuta è di produzione su un file di produzione, ed è dell'ultimo invio della fattura.
    /// </summary>
    private async Task ApplicaAsync(TrasmissioneFattura trasmissione, LettoreXmlSdi.RicevutaLetta letta, bool prova, CancellationToken cancellationToken)
    {
        var esito = letta.Tipo switch
        {
            TipoRicevutaSdi.Consegna => EsitoTrasmissioneSdi.Consegnato,
            TipoRicevutaSdi.MancataConsegna or TipoRicevutaSdi.AttestazioneTrasmissione => EsitoTrasmissioneSdi.NonConsegnato,
            TipoRicevutaSdi.Scarto => EsitoTrasmissioneSdi.Scartato,
            // Decorrenza termini ed esito arrivano dopo la consegna e non la smentiscono: si annotano.
            _ => (EsitoTrasmissioneSdi?)null,
        };

        // Uno scarto per nome file duplicato (00002) su un file già consegnato è il doppione di un
        // reinvio dopo una chiamata senza risposta: il primo è arrivato, la fattura resta consegnata.
        var doppioneDiUnConsegnato = letta.Tipo == TipoRicevutaSdi.Scarto
            && letta.Errori.Any(e => e.Codice == "00002")
            && trasmissione.Esito is EsitoTrasmissioneSdi.Consegnato or EsitoTrasmissioneSdi.NonConsegnato;

        var adesso = DateTime.UtcNow;
        if (esito is { } nuovo && !doppioneDiUnConsegnato)
        {
            trasmissione.Esito = nuovo;
            trasmissione.EsitoAtUtc = letta.DataOra?.UtcDateTime ?? adesso;
            trasmissione.Nota = letta.Testo;
            trasmissione.IdentificativoSdi ??= letta.IdentificativoSdi;
            trasmissione.UpdatedAtUtc = adesso;
        }

        var fattura = await fatture.GetAsync(trasmissione.DatiFatturaId, cancellationToken);
        // Collaudo e produzione non si mescolano mai: una ricevuta si applica solo a un file dello
        // stesso ambiente. Quelle di collaudo, solo nell'ambiente di test che lo chiede.
        var applicabile = prova == trasmissione.Prova && (!prova || canale.CollaudoApplicaEsiti)
            && fattura is not null && esito is not null && !doppioneDiUnConsegnato
            // Una ricevuta in ritardo su un file vecchio (scartato e poi rifatto) non riscrive lo stato
            // dell'invio più recente: si aggancia allo storico e basta.
            && (fattura.IdentificativoSdi is null || fattura.IdentificativoSdi == trasmissione.IdentificativoSdi);

        if (applicabile)
        {
            fattura!.StatoSdi = esito switch
            {
                EsitoTrasmissioneSdi.Consegnato => StatoSdiFattura.Consegnata,
                EsitoTrasmissioneSdi.NonConsegnato => StatoSdiFattura.NonConsegnata,
                _ => StatoSdiFattura.Scartata,
            };
            fattura.IdentificativoSdi ??= trasmissione.IdentificativoSdi;
            fattura.EsitoSdiAtUtc = trasmissione.EsitoAtUtc;
            fattura.NotaSdi = letta.Testo;
        }

        await sdi.SalvaAsync(cancellationToken);

        var etichetta = fattura is null ? $"file {trasmissione.NomeFile}" : $"Fattura n. {fattura.NumeroDocumento}/{fattura.Anno}";
        var struttura = await strutture.GetByIdAsync(trasmissione.StrutturaId, cancellationToken);
        await logEventi.RegistraAsync(
            letta.Tipo == TipoRicevutaSdi.Scarto ? LivelloLog.Warning : LivelloLog.Info,
            $"{(prova || trasmissione.Prova ? "[COLLAUDO] " : "")}{etichetta}: {Nome(letta.Tipo)} dallo SdI (file {trasmissione.NomeFile}).",
            origine: "SdI",
            dettaglio: letta.Testo,
            clienteId: struttura?.ClienteId,
            strutturaId: trasmissione.StrutturaId,
            categoria: "Fatturazione",
            cancellationToken: cancellationToken);

        if (applicabile)
        {
            // Solo a chi vede le finanze. Comunque nessun dato del cliente nel testo.
            var (titolo, messaggio) = letta.Tipo switch
            {
                TipoRicevutaSdi.Consegna => ($"{etichetta} consegnata", "Lo SdI ha recapitato la fattura al cliente."),
                TipoRicevutaSdi.Scarto => ($"{etichetta} scartata dallo SdI", "La fattura è stata rifiutata: correggila e inviala di nuovo, con lo stesso numero. Il motivo è nel dettaglio della fattura."),
                _ => ($"{etichetta} non consegnata", "La fattura è valida e si trova nel cassetto fiscale del cliente: consegnagli la copia PDF."),
            };
            await notifiche.CreaSeNonEsisteAsync(
                trasmissione.StrutturaId, TipoNotifica.EsitoFatturaSdi, $"sdi:esito:{trasmissione.NomeFile}:{letta.Tipo}", titolo, messaggio, cancellationToken, richiedeFinanze: true);
        }
    }

    private static string Nome(TipoRicevutaSdi tipo) => tipo switch
    {
        TipoRicevutaSdi.Consegna => "ricevuta di consegna",
        TipoRicevutaSdi.MancataConsegna => "impossibilità di recapito",
        TipoRicevutaSdi.Scarto => "ricevuta di scarto",
        TipoRicevutaSdi.Esito => "esito del committente",
        TipoRicevutaSdi.DecorrenzaTermini => "decorrenza termini",
        TipoRicevutaSdi.AttestazioneTrasmissione => "attestazione di trasmissione",
        _ => "messaggio non riconosciuto",
    };
}
