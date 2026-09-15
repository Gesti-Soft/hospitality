using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Notifiche;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.AlloggiatiWeb;

public record RisultatoInvioAlloggiatiWeb(int Inviate, int TotaleSchedine, int Errori, string? Messaggio);

/// <param name="ScadenzaInvioUtc">Termine di legge per la trasmissione (vedi TerminiSchedina); null se manca la data di arrivo.</param>
/// <param name="SoggiornoBreve">Soggiorno sotto le 24 ore: termine ridotto a 6 ore.</param>
/// <param name="InTermine">Ancora trasmissibile adesso. A false l'interfaccia non deve offrire l'invio: il portale lo rifiuterebbe.</param>
public record SchedinaAlloggiatiWeb(
    Guid OspiteId,
    Guid? PrenotazioneId,
    string NomeOspite,
    string? Camera,
    DateTime? CheckIn,
    DateTime? CheckOut,
    bool Inviata,
    DateTime? ScadenzaInvioUtc,
    bool SoggiornoBreve,
    bool InTermine);

/// <summary>
/// Invio giornaliero delle schedine Alloggiati Web — porta il ramo remoto di
/// StatePoliceLogic.SendSchedine del legacy: per ogni Ospite con soggiorno in corso non ancora
/// inviato (check-in oggi o ieri), genera un token e invia le righe della sua scheda (capofamiglia
/// + membri) con una chiamata Send dedicata, marcando la Prenotazione come inviata
/// (Prenotazione.StatePolice) solo in caso di successo. A differenza del legacy — che accumulava
/// tutte le righe di tutti gli ospiti del giorno in un'unica chiamata Send — qui ogni scheda è una
/// chiamata separata: un rifiuto su una prenotazione non blocca l'invio delle altre.
/// </summary>
public class AlloggiatiWebInvioService(
    IOspiteRepository ospiti,
    IPrenotazioneRepository prenotazioni,
    IAlloggiatiWebIntegrazioneRepository integrazioni,
    IAnagraficaAlloggiatiWebRepository anagrafica,
    IAlloggiatiWebClient client,
    IStrutturaRepository strutture,
    PermessoStrutturaGuard permessoGuard,
    ConcessioneServiziGuard concessioneGuard,
    ILogEventoService logEventi,
    NotificaService notificaService)
{
    public async Task<RisultatoInvioAlloggiatiWeb> InviaOraAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceWrite, cancellationToken);
        return await InviaSistemaAsync(strutturaId, cancellationToken);
    }

    /// <summary>Usato dal job Quartz schedulato (Worker) — nessun ICurrentUser, gira per conto del sistema.</summary>
    public async Task<RisultatoInvioAlloggiatiWeb> InviaSistemaAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        await concessioneGuard.EnsureAlloggiatiWebAsync(strutturaId, cancellationToken);

        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new AlloggiatiWebIntegrazione { StrutturaId = strutturaId };

        if (string.IsNullOrWhiteSpace(integrazione.Utente) || string.IsNullOrWhiteSpace(integrazione.Password) || string.IsNullOrWhiteSpace(integrazione.WsKey))
        {
            return await SalvaEsitoAsync(integrazione, 0, 0, "Credenziali Alloggiati Web non configurate.", esitoTentativo: EsitoTentativo.ErroreDefinitivo, cancellationToken);
        }

        var candidate = await ospiti.ListDaInviareAlloggiatiWebAsync(strutturaId, cancellationToken);

        // I termini di legge (24 ore dall'arrivo, 6 per i soggiorni brevi — vedi TerminiSchedina)
        // sono perentori: passati quelli il portale rifiuta la trasmissione, quindi continuare a
        // ritentarle produce solo errori a ripetizione. Vengono tolte dall'invio e segnalate una
        // volta sola per prenotazione: restano un obbligo di legge da assolvere a mano sul portale,
        // e sparire in silenzio sarebbe la cosa peggiore.
        var adesso = DateTime.UtcNow;
        var daInviare = candidate.Where(o => o.Prenotazione is null || TerminiSchedina.IsInTermine(o.Prenotazione, adesso)).ToList();
        var fuoriTermine = candidate.Where(o => o.Prenotazione is not null && !TerminiSchedina.IsInTermine(o.Prenotazione, adesso)).ToList();

        foreach (var scaduta in fuoriTermine)
        {
            await SegnalaFuoriTermineAsync(strutturaId, scaduta, cancellationToken);
        }

        if (daInviare.Count == 0)
        {
            return await SalvaEsitoAsync(integrazione, 0, 0, null, esitoTentativo: EsitoTentativo.Riuscito, cancellationToken);
        }

        var tokenRisultato = await client.GenerateTokenAsync(integrazione.Utente, integrazione.Password, integrazione.WsKey, cancellationToken);
        if (!tokenRisultato.Ok || tokenRisultato.Token is null)
        {
            return await SalvaEsitoAsync(integrazione, 0, daInviare.Count, tokenRisultato.Errore ?? "Token non ottenuto.", esitoTentativo: EsitoTentativo.ErroreRitentabile, cancellationToken);
        }

        var builder = await CreaBuilderAsync(cancellationToken);

        int inviate = 0, errori = 0;
        string? ultimoErrore = null;

        foreach (var ospite in daInviare)
        {
            try
            {
                var righe = builder.Costruisci(ospite);
                var esito = await client.SendAsync(integrazione.Utente, tokenRisultato.Token, righe, cancellationToken);

                if (!esito.Ok)
                {
                    errori++;
                    ultimoErrore = esito.ErroreDescrizione ?? esito.ErroreCodice ?? "Invio rifiutato.";
                    continue;
                }

                await MarcaInviataAsync(ospite.PrenotazioneId, cancellationToken);
                inviate++;
            }
            catch (Exception ex)
            {
                errori++;
                ultimoErrore = ex.Message;
            }
        }

        var messaggio = errori == 0 ? null : $"{errori} schedina/e non inviata/e: {ultimoErrore}";
        // Un rifiuto per singola schedina (dato non valido, doppione) non si risolve ritentando
        // l'intero batch, ma se almeno una è passata il portale risponde: si riprova solo quando
        // non ne è passata nessuna, il caso che somiglia a un problema del servizio.
        var esitoBatch = errori == 0
            ? EsitoTentativo.Riuscito
            : inviate > 0 ? EsitoTentativo.ErroreDefinitivo : EsitoTentativo.ErroreRitentabile;
        return await SalvaEsitoAsync(integrazione, inviate, daInviare.Count, messaggio, esitoBatch, cancellationToken);
    }

    /// <summary>
    /// Invio di una singola schedina, su richiesta esplicita dell'operatore: dal pulsante sulla riga
    /// della schermata Polizia di Stato e dalla conferma che compare al check-in di un soggiorno
    /// breve (termine di 6 ore, che il batch giornaliero non riuscirebbe a rispettare).
    /// Rifiuta l'invio fuori termine invece di tentarlo: il portale lo respingerebbe comunque, e un
    /// errore chiaro è più utile di un rifiuto tecnico.
    /// </summary>
    public async Task<RisultatoInvioAlloggiatiWeb> InviaSingolaAsync(ICurrentUser currentUser, Guid strutturaId, Guid ospiteId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceWrite, cancellationToken);
        await concessioneGuard.EnsureAlloggiatiWebAsync(strutturaId, cancellationToken);

        var ospite = await ospiti.GetConPrenotazioneAsync(strutturaId, ospiteId, cancellationToken)
            ?? throw new NotFoundException("Ospite non trovato.");

        if (ospite.Prenotazione is not { } prenotazione)
        {
            throw new ConflictException("La scheda non è collegata a nessuna prenotazione.");
        }

        if (prenotazione.StatePolice)
        {
            throw new ConflictException("Questa schedina risulta già inviata.");
        }

        if (!TerminiSchedina.IsInTermine(prenotazione, DateTime.UtcNow))
        {
            var ore = TerminiSchedina.IsSoggiornoBreve(prenotazione) ? 6 : 24;
            throw new ConflictException(
                $"Termine scaduto: la schedina andava trasmessa entro {ore} ore dall'arrivo. Va registrata a mano sul portale della Polizia di Stato.");
        }

        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new AlloggiatiWebIntegrazione { StrutturaId = strutturaId };

        if (string.IsNullOrWhiteSpace(integrazione.Utente) || string.IsNullOrWhiteSpace(integrazione.Password) || string.IsNullOrWhiteSpace(integrazione.WsKey))
        {
            throw new ConflictException("Credenziali Alloggiati Web non configurate.");
        }

        var tokenRisultato = await client.GenerateTokenAsync(integrazione.Utente, integrazione.Password, integrazione.WsKey, cancellationToken);
        if (!tokenRisultato.Ok || tokenRisultato.Token is null)
        {
            return await SalvaEsitoAsync(integrazione, 0, 1, tokenRisultato.Errore ?? "Token non ottenuto.", esitoTentativo: null, cancellationToken);
        }

        var builder = await CreaBuilderAsync(cancellationToken);
        var esito = await client.SendAsync(integrazione.Utente, tokenRisultato.Token, builder.Costruisci(ospite), cancellationToken);

        if (!esito.Ok)
        {
            return await SalvaEsitoAsync(integrazione, 0, 1, esito.ErroreDescrizione ?? esito.ErroreCodice ?? "Invio rifiutato.", esitoTentativo: null, cancellationToken);
        }

        await MarcaInviataAsync(prenotazione.Id, cancellationToken);
        return await SalvaEsitoAsync(integrazione, 1, 1, null, esitoTentativo: null, cancellationToken);
    }

    /// <summary>
    /// Notifica e log, una volta sola per prenotazione, di una schedina che ha superato il termine
    /// senza essere trasmessa — l'invio automatico non la prenderà più.
    /// </summary>
    private async Task SegnalaFuoriTermineAsync(Guid strutturaId, Ospite ospite, CancellationToken cancellationToken)
    {
        if (ospite.Prenotazione is not { } prenotazione)
        {
            return;
        }

        var ore = TerminiSchedina.IsSoggiornoBreve(prenotazione) ? 6 : 24;
        var nome = $"{ospite.Cognome} {ospite.Nome}".Trim();
        var creata = await notificaService.CreaPerPrenotazioneSeNonEsisteAsync(
            strutturaId, TipoNotifica.SchedinaFuoriTermine, prenotazione.Id,
            "Schedina fuori termine",
            $"La schedina di {nome} non è stata trasmessa entro {ore} ore dall'arrivo: va registrata a mano sul portale della Polizia di Stato.",
            cancellationToken);

        if (!creata)
        {
            return;
        }

        await logEventi.RegistraAsync(
            LivelloLog.Warning,
            $"Schedina di {nome} fuori termine ({ore} ore dall'arrivo): esclusa dall'invio automatico, da registrare a mano sul portale.",
            origine: "AlloggiatiWeb",
            clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
            strutturaId: strutturaId,
            categoria: "AlloggiatiWeb",
            cancellationToken: cancellationToken);
    }

    /// <summary>Elenco schedine dell'anno indicato per la schermata operativa — da inviare e già inviate, non solo quelle in coda.</summary>
    public async Task<IReadOnlyList<SchedinaAlloggiatiWeb>> ListSchedineAsync(ICurrentUser currentUser, Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var recenti = await ospiti.ListRecentiAlloggiatiWebAsync(strutturaId, anno, cancellationToken);

        var adesso = DateTime.UtcNow;

        return recenti.Select(o => new SchedinaAlloggiatiWeb(
            o.Id,
            o.PrenotazioneId,
            $"{o.Cognome} {o.Nome}".Trim(),
            o.Prenotazione?.Camera?.Nome,
            o.Prenotazione?.CheckIn,
            o.Prenotazione?.CheckOut,
            o.Prenotazione?.StatePolice ?? false,
            o.Prenotazione is null ? null : TerminiSchedina.ScadenzaUtc(o.Prenotazione),
            o.Prenotazione is not null && TerminiSchedina.IsSoggiornoBreve(o.Prenotazione),
            o.Prenotazione is not null && TerminiSchedina.IsInTermine(o.Prenotazione, adesso))).ToList();
    }

    /// <summary>Anni con almeno una prenotazione per il selettore Anno della schermata operativa — su richiesta esplicita, non deve proporre anni sicuramente vuoti.</summary>
    public async Task<IReadOnlyList<int>> ListaAnniAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);
        return await prenotazioni.ListaAnniConPrenotazioniAsync(strutturaId, cancellationToken);
    }

    /// <summary>
    /// Esportazione su richiesta (download) delle schedine ancora da inviare — fallback quando il
    /// servizio SOAP non è ancora configurato o non è raggiungibile, senza inviarle né marcarle
    /// come inviate (stesso pattern "on-demand, mai persistito" di PDF/XML fattura in Fase 4).
    /// Usa la stessa fonte dati della lista mostrata a schermo (stesso anno selezionato): l'export
    /// deve coincidere con quello che l'operatore vede in pagina come "da inviare".
    /// </summary>
    public async Task<string> EsportaAsync(ICurrentUser currentUser, Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var daInviare = await ListDaInviareRecentiAsync(strutturaId, anno, cancellationToken);
        var builder = await CreaBuilderAsync(cancellationToken);

        return string.Join("\r\n", daInviare.SelectMany(builder.Costruisci));
    }

    /// <summary>Esportazione di una singola schedina (per Ospite), oltre al bulk.</summary>
    public async Task<string> EsportaSingolaAsync(ICurrentUser currentUser, Guid strutturaId, Guid ospiteId, int anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var recenti = await ospiti.ListRecentiAlloggiatiWebAsync(strutturaId, anno, cancellationToken);
        var ospite = recenti.FirstOrDefault(o => o.Id == ospiteId)
            ?? throw new NotFoundException("Ospite non trovato tra le schedine dell'anno selezionato.");

        var builder = await CreaBuilderAsync(cancellationToken);
        return string.Join("\r\n", builder.Costruisci(ospite));
    }

    private async Task<IReadOnlyList<Ospite>> ListDaInviareRecentiAsync(Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        var recenti = await ospiti.ListRecentiAlloggiatiWebAsync(strutturaId, anno, cancellationToken);
        return recenti.Where(o => o.Prenotazione?.StatePolice != true).ToList();
    }

    private async Task<SchedinaAlloggiatiWebBuilder> CreaBuilderAsync(CancellationToken cancellationToken) => new(
        await anagrafica.ListLuoghiAsync(cancellationToken),
        await anagrafica.ListDocumentiAsync(cancellationToken),
        await anagrafica.ListTipiAlloggiatoAsync(cancellationToken));

    private async Task MarcaInviataAsync(Guid? prenotazioneId, CancellationToken cancellationToken)
    {
        if (prenotazioneId is not { } id)
        {
            return;
        }

        var prenotazione = await prenotazioni.GetAsync(id, cancellationToken);
        if (prenotazione is null)
        {
            return;
        }

        prenotazione.StatePolice = true;
        prenotazione.UpdatedAtUtc = DateTime.UtcNow;
        await prenotazioni.UpdateAsync(prenotazione, cancellationToken);
    }

    private async Task<RisultatoInvioAlloggiatiWeb> SalvaEsitoAsync(
        AlloggiatiWebIntegrazione integrazione,
        int inviate,
        int totale,
        string? errore,
        // null = invio singolo fatto a mano dall'operatore: non consuma i tentativi del job
        // automatico (non è lui ad aver fallito) e il suo esito va sempre a log, perché è la
        // risposta a un'azione appena compiuta da qualcuno che la sta guardando.
        EsitoTentativo? esitoTentativo,
        CancellationToken cancellationToken)
    {
        var adesso = DateTime.UtcNow;
        var tentativiEsauriti = esitoTentativo is { } esito && PoliticaTentativi.RegistraEsito(integrazione, esito, adesso);

        integrazione.UltimoInvioAtUtc = adesso;
        integrazione.UltimeSchedineInviate = inviate;
        integrazione.UltimoErrore = errore;
        await integrazioni.UpsertAsync(integrazione, cancellationToken);

        // Un log all'invio riuscito, anche "0/0 schedine" (nessuna da inviare oggi): l'utente deve
        // poter verificare dalla pagina Log che il job gira per questa struttura. I fallimenti
        // invece si annotano **solo all'ultimo tentativo utile**, con il conto di quelli spesi —
        // una riga per ogni ritentativo sarebbe lo stesso rumore che il limite serve a togliere.
        if (esitoTentativo is { } e && e != EsitoTentativo.Riuscito && !tentativiEsauriti)
        {
            return new RisultatoInvioAlloggiatiWeb(inviate, totale, totale - inviate, errore);
        }

        var resa = esitoTentativo is null
            ? string.Empty
            : $" ({DescriviTentativi(integrazione)}; nuovo tentativo domani all'orario configurato)";

        if (tentativiEsauriti && errore is not null)
        {
            await notificaService.CreaSeNonEsisteAsync(
                integrazione.StrutturaId,
                TipoNotifica.InvioSchedineNonRiuscito,
                $"invio-non-riuscito:AlloggiatiWeb:{integrazione.StrutturaId}:{adesso:yyyyMMdd}",
                "Polizia di Stato: invio non riuscito",
                $"{errore} Nuovo tentativo domani all'orario configurato — attenzione al termine di 24 ore dall'arrivo.",
                cancellationToken);
        }
        var messaggio = errore is null
            ? $"Invio Alloggiati Web (Polizia di Stato): {inviate}/{totale} schedine inviate."
            : $"Invio Alloggiati Web (Polizia di Stato): {inviate}/{totale} schedine inviate — {errore}{resa}.";

        await logEventi.RegistraAsync(
            errore is null ? LivelloLog.Info : LivelloLog.Warning,
            messaggio,
            origine: "AlloggiatiWeb",
            clienteId: await strutture.GetClienteIdAsync(integrazione.StrutturaId, cancellationToken),
            strutturaId: integrazione.StrutturaId,
            categoria: "AlloggiatiWeb",
            cancellationToken: cancellationToken);

        return new RisultatoInvioAlloggiatiWeb(inviate, totale, totale - inviate, errore);
    }

    /// <summary>Quanti tentativi sono stati spesi, per la riga di riepilogo: "1 tentativo" quando l'errore è di configurazione e non aveva senso ritentare.</summary>
    private static string DescriviTentativi(AlloggiatiWebIntegrazione integrazione) =>
        integrazione.TentativiFallitiOggi == 1
            ? "1 tentativo"
            : $"{integrazione.TentativiFallitiOggi} tentativi falliti";
}
