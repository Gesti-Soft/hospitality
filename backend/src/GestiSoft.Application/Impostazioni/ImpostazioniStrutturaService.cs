using GestiSoft.Application.Auth;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Impostazioni;

public record AggiornaImpostazioniRequest(
    bool PoliziaStatoAttiva,
    bool OsservatorioAttivo,
    bool PayTouristAttivo,
    TimeOnly? OraInvioGiornaliero,
    decimal? TassaSoggiornoPrezzo,
    int? TassaSoggiornoMaxGiorni,
    string? ComuneAttivita);

public class ImpostazioniStrutturaService(
    IImpostazioniStrutturaRepository repository,
    TenantAccessGuard accessGuard,
    ConcessioneServiziGuard concessioneGuard,
    GestioneUtentiGuard gestioneUtentiGuard)
{
    public async Task<ImpostazioniStruttura> GetOrDefaultAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);
        await gestioneUtentiGuard.EnsureAsync(currentUser, strutturaId, cancellationToken);

        return await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new ImpostazioniStruttura { StrutturaId = strutturaId };
    }

    public async Task<ImpostazioniStruttura> AggiornaAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        AggiornaImpostazioniRequest request,
        CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);
        await gestioneUtentiGuard.EnsureAsync(currentUser, strutturaId, cancellationToken);

        // La revoca del Super Admin prevale sempre sul toggle self-service: non basta nasconderlo
        // in UI, il salvataggio stesso va rifiutato se il servizio non è concesso al Cliente.
        if (request.PoliziaStatoAttiva)
        {
            await concessioneGuard.EnsureAlloggiatiWebAsync(strutturaId, cancellationToken);
        }
        if (request.OsservatorioAttivo)
        {
            await concessioneGuard.EnsureOsservatorioAsync(strutturaId, cancellationToken);
        }
        if (request.PayTouristAttivo)
        {
            await concessioneGuard.EnsurePayTouristAsync(strutturaId, cancellationToken);
        }

        var impostazioni = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new ImpostazioniStruttura { StrutturaId = strutturaId };

        impostazioni.PoliziaStatoAttiva = request.PoliziaStatoAttiva;
        impostazioni.OsservatorioAttivo = request.OsservatorioAttivo;
        impostazioni.PayTouristAttivo = request.PayTouristAttivo;
        impostazioni.OraInvioGiornaliero = request.OraInvioGiornaliero;
        impostazioni.TassaSoggiornoPrezzo = request.TassaSoggiornoPrezzo;
        impostazioni.TassaSoggiornoMaxGiorni = request.TassaSoggiornoMaxGiorni;
        impostazioni.ComuneAttivita = request.ComuneAttivita;
        impostazioni.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(impostazioni, cancellationToken);
        return impostazioni;
    }
}
