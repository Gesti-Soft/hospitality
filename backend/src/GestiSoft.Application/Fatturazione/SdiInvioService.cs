using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione;

/// <summary>Com'è andata la spedizione di una fattura.</summary>
/// <param name="Prova">Spedita allo SdI di collaudo: il giro è vero, la fattura resta da inviare.</param>
public record EsitoInvioSdi(string NomeFile, string IdentificativoSdi, bool Prova);

/// <summary>
/// La spedizione di una fattura allo SdI dal canale GestiSoft. Sempre su richiesta dell'operatore,
/// mai automatica. Non riceve un ICurrentUser: gira nel servizio SdI, che riceve la richiesta
/// dall'Api dopo che l'Api ha già controllato chi la fa (permesso FinanceWrite); qui si controlla
/// quello che vale per chiunque, cioè concessione, interruttore della struttura e stato della fattura.
/// </summary>
public class SdiInvioService(
    IDatiFatturaRepository fatture,
    IStrutturaRepository strutture,
    IImpostazioniStrutturaRepository impostazioni,
    ConcessioneServiziGuard concessioneGuard,
    FileSdiService fileSdi,
    ITrasmissioneFatturaRepository trasmissioni,
    ISdiTrasmissioneClient client,
    ILogEventoService logEventi)
{
    public async Task<EsitoInvioSdi> InviaAsync(Guid strutturaId, Guid fatturaId, string? operatore, CancellationToken cancellationToken)
    {
        await concessioneGuard.EnsureSdiAsync(strutturaId, cancellationToken);
        if (await impostazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken) is not { SdiAttivo: true })
        {
            throw new ConflictException("L'invio delle fatture allo SdI è spento nelle impostazioni della struttura.");
        }

        var fattura = await fatture.GetAsync(fatturaId, cancellationToken);
        if (fattura is null || fattura.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Fattura non trovata.");
        }

        VerificaSpedibile(fattura);

        var file = await fileSdi.PreparaAsync(fattura, viaCanale: true, cancellationToken);
        var etichetta = $"Fattura n. {fattura.NumeroDocumento}/{fattura.Anno}";

        RispostaRiceviFile risposta;
        try
        {
            risposta = await client.InviaAsync(file.NomeFile, file.Xml, cancellationToken);
        }
        catch (SdiNonRaggiungibileException ex)
        {
            file.Esito = EsitoTrasmissioneSdi.EsitoIgnoto;
            file.Nota = "Nessuna risposta dallo SdI: il file potrebbe essere arrivato.";
            file.InviataDa = operatore;
            // La fattura non cambia stato: a bloccarne la modifica è l'esito ignoto del file.
            await trasmissioni.SalvaAsync(cancellationToken);
            await RegistraAsync(fattura, LivelloLog.Warning, $"{etichetta}: nessuna risposta dallo SdI all'invio del file {file.NomeFile}.", ex.Message, operatore, cancellationToken);
            throw new ConflictException(
                "Lo SdI non ha risposto: il file potrebbe essere arrivato oppure no. Controlla sul portale Fatture e Corrispettivi prima di riprovare. " +
                "Riprovando si rispedisce lo stesso file: se era già arrivato, lo SdI lo scarta come doppione e la fattura non viene emessa due volte.");
        }

        if (!risposta.Accettato)
        {
            file.Esito = EsitoTrasmissioneSdi.Rifiutato;
            file.Nota = DescrizioneErrore(risposta.Errore);
            file.InviataDa = operatore;
            await trasmissioni.SalvaAsync(cancellationToken);
            await RegistraAsync(fattura, LivelloLog.Warning, $"{etichetta}: lo SdI ha rifiutato il file {file.NomeFile} ({risposta.Errore ?? "senza codice"}).", null, operatore, cancellationToken);
            throw new ConflictException($"Lo SdI non ha accettato il file: {file.Nota}");
        }

        var adesso = DateTime.UtcNow;
        file.Esito = EsitoTrasmissioneSdi.Inviato;
        file.IdentificativoSdi = risposta.IdentificativoSdi;
        file.InviataAtUtc = risposta.DataOraRicezione?.UtcDateTime ?? adesso;
        file.InviataDa = operatore;
        file.Nota = null;
        file.Prova = client.AmbienteDiProva;

        // In collaudo il file è partito davvero, ma verso uno SdI dove non ha valore: segnare la
        // fattura come inviata la farebbe sembrare trasmessa quando non è arrivata da nessuna parte.
        if (!file.Prova)
        {
            fattura.StatoSdi = StatoSdiFattura.Inviata;
            fattura.IdentificativoSdi = risposta.IdentificativoSdi;
            fattura.InviataSdiAtUtc = file.InviataAtUtc;
            fattura.EsitoSdiAtUtc = null;
            fattura.NotaSdi = null;
        }

        await trasmissioni.SalvaAsync(cancellationToken);
        await RegistraAsync(
            fattura,
            LivelloLog.Info,
            file.Prova
                ? $"{etichetta}: file {file.NomeFile} spedito allo SdI di COLLAUDO (identificativo {risposta.IdentificativoSdi}). La fattura resta da inviare."
                : $"{etichetta} inviata allo SdI: file {file.NomeFile}, identificativo {risposta.IdentificativoSdi}.",
            null,
            operatore,
            cancellationToken);

        return new EsitoInvioSdi(file.NomeFile, risposta.IdentificativoSdi!, file.Prova);
    }

    /// <summary>
    /// Si spediscono solo le fatture nate col canale (le precedenti possono essere già state caricate
    /// a mano sul portale), non ancora nello SdI.
    /// </summary>
    private static void VerificaSpedibile(DatiFattura fattura)
    {
        if (fattura.TipoEmissione == TipoEmissioneDocumento.Ricevuta)
        {
            throw new ConflictException("Una ricevuta di locazione breve non passa dallo SdI.");
        }

        switch (fattura.StatoSdi)
        {
            case null:
                throw new ConflictException("Questa fattura è stata emessa prima dell'invio allo SdI dal gestionale: se serve, scarica l'XML e caricalo dal portale Fatture e Corrispettivi.");
            case StatoSdiFattura.Inviata:
                throw new ConflictException("La fattura è già stata inviata allo SdI: si attende la ricevuta.");
            case StatoSdiFattura.Consegnata or StatoSdiFattura.NonConsegnata:
                throw new ConflictException("La fattura è già stata trasmessa allo SdI. Per correggerla emetti una nota di credito.");
        }
    }

    /// <summary>I codici di errore di RiceviFile, in parole.</summary>
    internal static string DescrizioneErrore(string? codice) => codice switch
    {
        "EI01" => "file vuoto (EI01).",
        "EI02" => "servizio dello SdI momentaneamente non disponibile (EI02). Riprova più tardi.",
        "EI03" => "canale non abilitato alla trasmissione (EI03). Contatta l'assistenza.",
        null or "" => "risposta senza identificativo né codice di errore.",
        _ when codice.StartsWith("EI", StringComparison.Ordinal) => $"errore {codice}.",
        _ => codice,
    };

    private async Task RegistraAsync(DatiFattura fattura, LivelloLog livello, string messaggio, string? dettaglio, string? operatore, CancellationToken cancellationToken)
    {
        var struttura = await strutture.GetByIdAsync(fattura.StrutturaId, cancellationToken);
        await logEventi.RegistraAsync(
            livello,
            messaggio,
            origine: "SdI",
            dettaglio: dettaglio,
            clienteId: struttura?.ClienteId,
            strutturaId: fattura.StrutturaId,
            categoria: "Fatturazione",
            operatore: operatore,
            cancellationToken: cancellationToken);
    }
}
