using GestiSoft.Application.Auth;

namespace GestiSoft.Application.Wubook;

public record CreaPianoRestrizioneRequest(string Nome, WubookRegoleRestrizione? Regole);

public record AggiornaPianoRestrizioneRequest(string? Nome, WubookRegoleRestrizione? Regole);

/// <summary>
/// Piani restrizione nominati (regole di default per un piano: min/max stay, chiusure arrivo-
/// partenza) — porta RestrictionsController del legacy. Puro proxy verso Wubook, nessuna
/// persistenza locale (stesso principio di <see cref="WubookPianiPrezzoService"/>). Distinto dalle
/// restrizioni per-camera/per-periodo di <see cref="WubookRestrizioniPeriodoService"/>, che scrivono
/// valori giorno-per-giorno sul piano di default (pid=0) invece di gestire i piani stessi.
/// </summary>
public class WubookPianiRestrizioneService(IWubookClient wubookClient, WubookLicenzaService licenzaService, PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<WubookPianoRestrizione>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        return await wubookClient.GetRestrictionPlansAsync(token, lcode, cancellationToken);
    }

    public async Task<int> CreaAsync(ICurrentUser currentUser, Guid strutturaId, CreaPianoRestrizioneRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);

        var pianoId = await wubookClient.AddRestrictionPlanAsync(token, lcode, request.Nome, cancellationToken);
        if (request.Regole is { } regole)
        {
            await wubookClient.UpdateRestrictionPlanRulesAsync(token, lcode, pianoId, regole, cancellationToken);
        }

        return pianoId;
    }

    public async Task AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, int pianoId, AggiornaPianoRestrizioneRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);

        if (request.Nome is { } nome)
        {
            await wubookClient.RenameRestrictionPlanAsync(token, lcode, pianoId, nome, cancellationToken);
        }

        if (request.Regole is { } regole)
        {
            await wubookClient.UpdateRestrictionPlanRulesAsync(token, lcode, pianoId, regole, cancellationToken);
        }
    }

    public async Task EliminaAsync(ICurrentUser currentUser, Guid strutturaId, int pianoId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        await wubookClient.DelRestrictionPlanAsync(token, lcode, pianoId, cancellationToken);
    }
}
