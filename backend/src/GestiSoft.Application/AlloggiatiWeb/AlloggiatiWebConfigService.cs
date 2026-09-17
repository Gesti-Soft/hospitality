using GestiSoft.Application.Auth;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.AlloggiatiWeb;

public record AggiornaAlloggiatiWebConfigRequest(string? Utente, string? Password, string? WsKey);

/// <summary>
/// Configurazione delle credenziali Alloggiati Web per Struttura. L'attivazione del servizio e
/// l'orario di invio restano su ImpostazioniStruttura (PoliziaStatoAttiva/OraInvioGiornaliero, già
/// gestiti da ImpostazioniStrutturaService dalla Fase 2) — qui si gestiscono solo le credenziali.
/// </summary>
public class AlloggiatiWebConfigService(
    IAlloggiatiWebIntegrazioneRepository repository,
    IAlloggiatiWebClient client,
    IStrutturaRepository strutture,
    ILogEventoService logEventi,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<AlloggiatiWebIntegrazione> GetOrDefaultAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        return await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new AlloggiatiWebIntegrazione { StrutturaId = strutturaId };
    }

    public async Task<(AlloggiatiWebIntegrazione Integrazione, bool ConnessioneOk, string? ConnessioneErrore)> AggiornaConfigAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaAlloggiatiWebConfigRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new AlloggiatiWebIntegrazione { StrutturaId = strutturaId };

        entity.Utente = request.Utente;

        // Password e Ws Key vuote = "non toccarle", mai "azzerale": non tornano mai al client, il
        // form le mostra sempre vuote, e un salvataggio fatto per correggere l'utente cancellerebbe
        // credenziali funzionanti senza averlo chiesto (richiesta esplicita dell'utente).
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            entity.Password = request.Password;
        }

        if (!string.IsNullOrWhiteSpace(request.WsKey))
        {
            entity.WsKey = request.WsKey;
        }

        entity.UpdatedAtUtc = DateTime.UtcNow;

        var (ok, errore) = await VerificaConnessioneAsync(entity, cancellationToken);
        if (ok)
        {
            entity.UltimaVerificaOkAtUtc = DateTime.UtcNow;
        }

        await repository.UpsertAsync(entity, cancellationToken);

        await logEventi.RegistraAsync(
            ok ? LivelloLog.Info : LivelloLog.Warning,
            ok ? "Verifica connessione Alloggiati Web riuscita." : $"Verifica connessione Alloggiati Web non riuscita: {errore}",
            origine: "AlloggiatiWeb",
            clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
            strutturaId: strutturaId,
            categoria: "AlloggiatiWeb",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

        return (entity, ok, errore);
    }

    /// <summary>
    /// Test di connessione reale eseguito subito dopo il salvataggio delle credenziali, su richiesta
    /// esplicita dell'utente — invece di scoprire Utente/Password/WsKey sbagliati solo al primo
    /// invio giornaliero reale. GenerateToken è la stessa operazione SOAP già usata da
    /// AlloggiatiWebInvioService prima di ogni invio, qui chiamata da sola (nessuna schedina inviata).
    /// </summary>
    private async Task<(bool Ok, string? Errore)> VerificaConnessioneAsync(AlloggiatiWebIntegrazione entity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entity.Utente) || string.IsNullOrWhiteSpace(entity.Password) || string.IsNullOrWhiteSpace(entity.WsKey))
        {
            return (false, "Utente, password e WsKey sono obbligatori per la verifica.");
        }

        var risultato = await client.GenerateTokenAsync(entity.Utente, entity.Password, entity.WsKey, cancellationToken);
        return (risultato.Ok, risultato.Ok ? null : risultato.Errore ?? "Credenziali non valide.");
    }
}
