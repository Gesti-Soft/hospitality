using GestiSoft.Application.Auth;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.AlloggiatiWeb;

public record RisultatoInvioAlloggiatiWeb(int Inviate, int TotaleSchedine, int Errori, string? Messaggio);

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
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<RisultatoInvioAlloggiatiWeb> InviaOraAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceWrite, cancellationToken);
        return await InviaSistemaAsync(strutturaId, cancellationToken);
    }

    /// <summary>Usato dal job Quartz schedulato (Worker) — nessun ICurrentUser, gira per conto del sistema.</summary>
    public async Task<RisultatoInvioAlloggiatiWeb> InviaSistemaAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// Esportazione su richiesta (download) delle schedine del giorno — fallback quando il
    /// servizio SOAP non è ancora configurato o non è raggiungibile, senza inviarle né marcarle
    /// come inviate (stesso pattern "on-demand, mai persistito" di PDF/XML fattura in Fase 4).
    /// </summary>
    public async Task<string> EsportaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var daInviare = await ospiti.ListDaInviareAlloggiatiWebAsync(strutturaId, cancellationToken);
        var builder = await CreaBuilderAsync(cancellationToken);

        return string.Join("\r\n", daInviare.SelectMany(builder.Costruisci));
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

        return new RisultatoInvioAlloggiatiWeb(inviate, totale, totale - inviate, errore);
    }
}
