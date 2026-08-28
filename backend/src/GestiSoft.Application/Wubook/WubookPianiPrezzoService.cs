using GestiSoft.Application.Auth;

namespace GestiSoft.Application.Wubook;

public record CreaPianoPrezzoRequest(string Nome, int ParentId, int TipoVariazione, decimal Variazione);

public record AggiornaPianoPrezzoRequest(string? Nome, int? TipoVariazione, decimal? Variazione);

/// <summary>
/// Piani prezzo nominati/virtuali (es. "Non rimborsabile -10%") — porta PricingPlansController del
/// legacy. Puro proxy verso Wubook: nessuna persistenza locale, esattamente come nel legacy (la
/// mappatura piano→canale si fa nel pannello Wubook stesso, non qui). Solo il piano di default
/// (pid=0, Parity) resta gestito da <see cref="WubookPrezziService"/> per la sincronizzazione
/// automatica giornaliera.
/// </summary>
public class WubookPianiPrezzoService(IWubookClient wubookClient, WubookLicenzaService licenzaService, PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<WubookPianoPrezzo>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        return await wubookClient.GetPricingPlansAsync(token, lcode, cancellationToken);
    }

    public async Task<int> CreaAsync(ICurrentUser currentUser, Guid strutturaId, CreaPianoPrezzoRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        return await wubookClient.AddVirtualPlanAsync(token, lcode, request.Nome, request.ParentId, request.TipoVariazione, request.Variazione, cancellationToken);
    }

    public async Task AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, int pianoId, AggiornaPianoPrezzoRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);

        if (request.Nome is { } nome)
        {
            await wubookClient.UpdatePlanNameAsync(token, lcode, pianoId, nome, cancellationToken);
        }

        if (request.TipoVariazione is { } tipoVariazione && request.Variazione is { } variazione)
        {
            await wubookClient.ModVirtualPlanAsync(token, lcode, pianoId, tipoVariazione, variazione, cancellationToken);
        }
    }

    public async Task EliminaAsync(ICurrentUser currentUser, Guid strutturaId, int pianoId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        await wubookClient.DelPlanAsync(token, lcode, pianoId, cancellationToken);
    }
}
