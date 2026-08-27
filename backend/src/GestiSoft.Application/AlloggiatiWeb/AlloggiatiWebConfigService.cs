using GestiSoft.Application.Auth;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.AlloggiatiWeb;

public record AggiornaAlloggiatiWebConfigRequest(string? Utente, string? Password, string? WsKey);

/// <summary>
/// Configurazione delle credenziali Alloggiati Web per Struttura. L'attivazione del servizio e
/// l'orario di invio restano su ImpostazioniStruttura (PoliziaStatoAttiva/OraInvioGiornaliero, già
/// gestiti da ImpostazioniStrutturaService dalla Fase 2) — qui si gestiscono solo le credenziali.
/// </summary>
public class AlloggiatiWebConfigService(
    IAlloggiatiWebIntegrazioneRepository repository,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<AlloggiatiWebIntegrazione> GetOrDefaultAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        return await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new AlloggiatiWebIntegrazione { StrutturaId = strutturaId };
    }

    public async Task<AlloggiatiWebIntegrazione> AggiornaConfigAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaAlloggiatiWebConfigRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new AlloggiatiWebIntegrazione { StrutturaId = strutturaId };

        entity.Utente = request.Utente;
        entity.Password = request.Password;
        entity.WsKey = request.WsKey;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(entity, cancellationToken);
        return entity;
    }
}
