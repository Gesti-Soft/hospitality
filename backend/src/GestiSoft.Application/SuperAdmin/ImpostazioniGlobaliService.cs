using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.SuperAdmin;

public record AggiornaImpostazioniGlobaliRequest(int? IdSoftwarePaytourist, string? TokenWubook);

/// <summary>Impostazioni a livello di applicazione (non di Cliente/Struttura) — solo il Super Admin le gestisce.</summary>
public class ImpostazioniGlobaliService(IImpostazioniGlobaliRepository repository)
{
    public async Task<ImpostazioniGlobali> GetAsync(ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await repository.GetAsync(cancellationToken) ?? new ImpostazioniGlobali();
    }

    public async Task<ImpostazioniGlobali> AggiornaAsync(ICurrentUser currentUser, AggiornaImpostazioniGlobaliRequest request, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var entity = await repository.GetAsync(cancellationToken) ?? new ImpostazioniGlobali();
        entity.IdSoftwarePaytourist = request.IdSoftwarePaytourist;
        entity.TokenWubook = request.TokenWubook;

        await repository.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>Usato internamente da WubookLicenzaService.GetIdPaytouristAsync — nessun controllo permessi, non è un endpoint diretto.</summary>
    internal async Task<int?> GetIdSoftwarePaytouristAsync(CancellationToken cancellationToken) =>
        (await repository.GetAsync(cancellationToken))?.IdSoftwarePaytourist;

    /// <summary>Usato internamente da WubookLicenzaService.GetCredenzialiValideAsync — nessun controllo permessi, non è un endpoint diretto.</summary>
    internal async Task<string?> GetTokenWubookAsync(CancellationToken cancellationToken) =>
        (await repository.GetAsync(cancellationToken))?.TokenWubook;

    private static void RichiediSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può gestire le impostazioni globali.");
        }
    }
}
