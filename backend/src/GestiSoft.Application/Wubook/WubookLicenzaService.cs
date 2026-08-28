using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public record AggiornaWubookConfigRequest(bool Attivo);

public record AggiornaWubookLicenzaRequest(string? GestisoftUsername, string? GestisoftToken);

/// <summary>
/// Configurazione dell'integrazione Wubook per Struttura e rinnovo delle credenziali. Il rinnovo
/// (<see cref="RinnovaCredenzialiAsync"/>) è la traduzione diretta di GetWobook/ValidateUserCredentials
/// del legacy: chiama sempre gestisoft.it, non genera/salva mai tokenWb o idWoBook localmente.
/// </summary>
public class WubookLicenzaService(
    IWubookIntegrazioneRepository repository,
    IGestisoftLicenzaClient licenzaClient,
    PermessoStrutturaGuard permessoGuard)
{
    /// <summary>Soglia oltre la quale la cache credenziali è considerata scaduta e va rinnovata prima di operare — il job periodico (ogni ~2h) dovrebbe sempre restare sotto questa soglia in condizioni normali.</summary>
    private static readonly TimeSpan ScadenzaCache = TimeSpan.FromHours(3);

    public async Task<WubookIntegrazione> GetOrDefaultAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);

        return await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new WubookIntegrazione { StrutturaId = strutturaId };
    }

    public async Task<WubookIntegrazione> AggiornaConfigAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaWubookConfigRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new WubookIntegrazione { StrutturaId = strutturaId };

        entity.Attivo = request.Attivo;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>
    /// Separato da <see cref="AggiornaConfigAsync"/> apposta: le due form (toggle Attivo e licenza
    /// gestisoft.it) vivono entrambe in ImpostazioniPage ma sono submit indipendenti — un unico
    /// endpoint che scrivesse insieme tutti i campi rischierebbe di azzerare il token già salvato
    /// se l'operatore tocca solo il toggle, o viceversa.
    /// </summary>
    public async Task<WubookIntegrazione> AggiornaLicenzaAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaWubookLicenzaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new WubookIntegrazione { StrutturaId = strutturaId };

        entity.GestisoftUsername = request.GestisoftUsername;
        entity.GestisoftToken = request.GestisoftToken;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>Chiamato dal job periodico (tutte le strutture attive) e on-demand prima di operazioni Wubook se la cache è scaduta.</summary>
    public async Task<WubookIntegrazione> RinnovaCredenzialiAsync(WubookIntegrazione integrazione, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(integrazione.GestisoftUsername) || string.IsNullOrWhiteSpace(integrazione.GestisoftToken))
        {
            integrazione.UltimoErrore = "Licenza gestisoft.it non configurata (username/token mancanti).";
            await repository.UpsertAsync(integrazione, cancellationToken);
            return integrazione;
        }

        var risultato = await licenzaClient.SetRunningAsync(integrazione.GestisoftUsername, integrazione.GestisoftToken, isRunning: true, cancellationToken);

        if (risultato.Status != "ok")
        {
            integrazione.UltimoErrore = risultato.Messaggio ?? $"Licenza non valida (stato: {risultato.Status}).";
            await repository.UpsertAsync(integrazione, cancellationToken);
            return integrazione;
        }

        integrazione.ApiKeyCache = risultato.TokenWb;
        integrazione.LcodeCache = risultato.IdWoBook;
        integrazione.IdPaytouristCache = risultato.IdPaytourist;
        integrazione.CacheAggiornataAtUtc = DateTime.UtcNow;
        integrazione.UltimoErrore = null;

        await repository.UpsertAsync(integrazione, cancellationToken);
        return integrazione;
    }

    /// <summary>Credenziali pronte all'uso per una Struttura — rinnova al volo se la cache è scaduta o assente.</summary>
    public async Task<(string Token, string Lcode)> GetCredenzialiValideAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var integrazione = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? throw new ConflictException("Integrazione Wubook non configurata per questa struttura.");

        if (!integrazione.Attivo)
        {
            throw new ConflictException("Integrazione Wubook non attiva per questa struttura.");
        }

        var cacheValida = integrazione.CacheAggiornataAtUtc is { } aggiornata && DateTime.UtcNow - aggiornata < ScadenzaCache;
        if (!cacheValida)
        {
            integrazione = await RinnovaCredenzialiAsync(integrazione, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(integrazione.ApiKeyCache) || string.IsNullOrWhiteSpace(integrazione.LcodeCache))
        {
            throw new ConflictException(integrazione.UltimoErrore ?? "Credenziali Wubook non disponibili.");
        }

        return (integrazione.ApiKeyCache, integrazione.LcodeCache);
    }

    /// <summary>
    /// Id Software PayTourist (Fase 8) — stessa cache/rinnovo di <see cref="GetCredenzialiValideAsync"/>
    /// ma, a differenza di quella, NON richiede <see cref="WubookIntegrazione.Attivo"/> (che gate
    /// solo la sincronizzazione Wubook): basta che la licenza gestisoft.it (username/token) sia
    /// configurata su questa Struttura, indipendentemente dal fatto che usi anche Wubook o meno —
    /// idPaytourist arriva dalla stessa risposta di set-running ma è concettualmente un dato di
    /// licenza generale, non specifico di Wubook.
    /// </summary>
    public async Task<int> GetIdPaytouristAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var integrazione = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? throw new ConflictException("Licenza gestisoft.it non configurata per questa struttura.");

        var cacheValida = integrazione.CacheAggiornataAtUtc is { } aggiornata && DateTime.UtcNow - aggiornata < ScadenzaCache;
        if (!cacheValida)
        {
            integrazione = await RinnovaCredenzialiAsync(integrazione, cancellationToken);
        }

        if (!int.TryParse(integrazione.IdPaytouristCache, out var idPaytourist) || idPaytourist <= 0)
        {
            throw new ConflictException(integrazione.UltimoErrore ?? "Id Software PayTourist non disponibile per questa struttura.");
        }

        return idPaytourist;
    }
}
