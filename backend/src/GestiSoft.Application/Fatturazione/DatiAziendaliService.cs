using GestiSoft.Application.Auth;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione;

public record AggiornaDatiAziendaliRequest(
    string? Iso2,
    string? PIva,
    string? CodiceFiscale,
    string? Denominazione,
    string? Nome,
    string? Cognome,
    RegimeFiscale? RegimeFiscale,
    AliquotaIva? AliquotaIvaDefault,
    NaturaIva? NaturaDefault,
    string? Indirizzo,
    string? NCivico,
    string? Cap,
    string? Comune,
    string? Provincia,
    string? Nazione);

/// <summary>
/// Profilo fiscale emittente della struttura (una sola riga per Struttura) — porta
/// Settings.GetDataAzienda()/AddOrUpdateDataAzienda del legacy.
/// </summary>
public class DatiAziendaliService(IDatiAziendaliRepository repository, PermessoStrutturaGuard permessoGuard)
{
    public async Task<DatiAziendali> GetOrDefaultAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);

        return await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new DatiAziendali { StrutturaId = strutturaId };
    }

    public async Task<DatiAziendali> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaDatiAziendaliRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new DatiAziendali { StrutturaId = strutturaId };

        entity.Iso2 = request.Iso2;
        entity.PIva = request.PIva;
        entity.CodiceFiscale = request.CodiceFiscale;
        entity.Denominazione = request.Denominazione;
        entity.Nome = request.Nome;
        entity.Cognome = request.Cognome;
        entity.RegimeFiscale = request.RegimeFiscale;
        entity.AliquotaIvaDefault = request.AliquotaIvaDefault;
        entity.NaturaDefault = request.NaturaDefault;
        entity.Indirizzo = request.Indirizzo;
        entity.NCivico = request.NCivico;
        entity.Cap = request.Cap;
        entity.Comune = request.Comune;
        entity.Provincia = request.Provincia;
        entity.Nazione = request.Nazione;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(entity, cancellationToken);
        return entity;
    }
}
