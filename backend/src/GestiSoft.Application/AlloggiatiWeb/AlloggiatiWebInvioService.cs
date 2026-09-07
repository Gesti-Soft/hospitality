using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.AlloggiatiWeb;

public record RisultatoInvioAlloggiatiWeb(int Inviate, int TotaleSchedine, int Errori, string? Messaggio);

public record SchedinaAlloggiatiWeb(Guid OspiteId, Guid? PrenotazioneId, string NomeOspite, string? Camera, DateTime? CheckIn, DateTime? CheckOut, bool Inviata);

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
    ILogEventoService logEventi)
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
            return await SalvaEsitoAsync(integrazione, 0, 0, "Credenziali Alloggiati Web non configurate.", cancellationToken);
        }

        var daInviare = await ospiti.ListDaInviareAlloggiatiWebAsync(strutturaId, cancellationToken);
        if (daInviare.Count == 0)
        {
            return await SalvaEsitoAsync(integrazione, 0, 0, null, cancellationToken);
        }

        var tokenRisultato = await client.GenerateTokenAsync(integrazione.Utente, integrazione.Password, integrazione.WsKey, cancellationToken);
        if (!tokenRisultato.Ok || tokenRisultato.Token is null)
        {
            return await SalvaEsitoAsync(integrazione, 0, daInviare.Count, tokenRisultato.Errore ?? "Token non ottenuto.", cancellationToken);
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
        return await SalvaEsitoAsync(integrazione, inviate, daInviare.Count, messaggio, cancellationToken);
    }

    /// <summary>Elenco schedine dell'anno indicato per la schermata operativa — da inviare e già inviate, non solo quelle in coda.</summary>
    public async Task<IReadOnlyList<SchedinaAlloggiatiWeb>> ListSchedineAsync(ICurrentUser currentUser, Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var recenti = await ospiti.ListRecentiAlloggiatiWebAsync(strutturaId, anno, cancellationToken);

        return recenti.Select(o => new SchedinaAlloggiatiWeb(
            o.Id,
            o.PrenotazioneId,
            $"{o.Cognome} {o.Nome}".Trim(),
            o.Prenotazione?.Camera?.Nome,
            o.Prenotazione?.CheckIn,
            o.Prenotazione?.CheckOut,
            o.Prenotazione?.StatePolice ?? false)).ToList();
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

    private async Task<RisultatoInvioAlloggiatiWeb> SalvaEsitoAsync(AlloggiatiWebIntegrazione integrazione, int inviate, int totale, string? errore, CancellationToken cancellationToken)
    {
        integrazione.UltimoInvioAtUtc = DateTime.UtcNow;
        integrazione.UltimeSchedineInviate = inviate;
        integrazione.UltimoErrore = errore;
        await integrazioni.UpsertAsync(integrazione, cancellationToken);

        // Un log ad ogni invio, anche "0/0 schedine" (nessuna da inviare oggi) — l'utente deve poter
        // verificare dalla pagina Log che il job gira regolarmente per questa struttura, non solo
        // quando c'è stato un errore o un invio reale.
        var messaggio = errore is null
            ? $"Invio Alloggiati Web (Polizia di Stato): {inviate}/{totale} schedine inviate."
            : $"Invio Alloggiati Web (Polizia di Stato): {inviate}/{totale} schedine inviate — {errore}";

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
}
