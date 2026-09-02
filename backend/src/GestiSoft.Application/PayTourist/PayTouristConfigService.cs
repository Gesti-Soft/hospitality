using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.PayTourist;

public record AggiornaPayTouristConfigRequest(string? Token, bool PortaleOnlineAttivo);

public record SalvaPayTouristStrutturaRequest(string? Nome, int? IdStrutturaPaytourist, IReadOnlyList<Guid> TipologieIds);

/// <summary>
/// Configurazione PayTourist per Struttura: il token (segreto, condiviso) e l'elenco delle
/// "strutture" PayTourist configurate (una Struttura di questo gestionale può averne più di una,
/// ognuna instrada un sottoinsieme di tipologie camera — porta PayTouristUser del legacy, stesso
/// pattern CRUD già usato per gli Appartamenti Osservatorio Turistico in Fase 7). Nessun flag di
/// permesso dedicato "PayTourist" nel modello UtenteStruttura: riusa StatePoliceSettings/
/// StatePoliceWrite/StatePoliceRead, stesso riuso pragmatico già fatto per Wubook/Alloggiati
/// Web/Osservatorio.
/// </summary>
public class PayTouristConfigService(
    IPayTouristIntegrazioneRepository integrazioni,
    IPayTouristStrutturaRepository strutture,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<PayTouristIntegrazione> GetConfigAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        return await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new PayTouristIntegrazione { StrutturaId = strutturaId };
    }

    public async Task<PayTouristIntegrazione> AggiornaConfigAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaPayTouristConfigRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new PayTouristIntegrazione { StrutturaId = strutturaId };

        entity.Token = request.Token;
        entity.PortaleOnlineAttivo = request.PortaleOnlineAttivo;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await integrazioni.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<PayTouristStruttura>> ListaStruttureAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);
        return await strutture.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<PayTouristStruttura> CreaStrutturaAsync(ICurrentUser currentUser, Guid strutturaId, SalvaPayTouristStrutturaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = new PayTouristStruttura { StrutturaId = strutturaId };
        Applica(entity, request);

        await strutture.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<PayTouristStruttura> AggiornaStrutturaAsync(ICurrentUser currentUser, Guid strutturaId, Guid payTouristStrutturaId, SalvaPayTouristStrutturaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await strutture.GetAsync(strutturaId, payTouristStrutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura PayTourist non trovata.");

        Applica(entity, request);

        await strutture.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    public async Task EliminaStrutturaAsync(ICurrentUser currentUser, Guid strutturaId, Guid payTouristStrutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await strutture.GetAsync(strutturaId, payTouristStrutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura PayTourist non trovata.");

        await strutture.DeleteAsync(entity, cancellationToken);
    }

    private void Applica(PayTouristStruttura entity, SalvaPayTouristStrutturaRequest request)
    {
        entity.Nome = request.Nome;
        entity.IdStrutturaPaytourist = request.IdStrutturaPaytourist;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        SincronizzaTipologie(entity, request.TipologieIds);
    }

    private void SincronizzaTipologie(PayTouristStruttura entity, IReadOnlyList<Guid> tipologieIds)
    {
        var richieste = tipologieIds.ToHashSet();

        foreach (var daRimuovere in entity.Tipologie.Where(t => !richieste.Contains(t.TipologiaId)).ToList())
        {
            entity.Tipologie.Remove(daRimuovere);
            strutture.RimuoviTipologia(daRimuovere);
        }

        var esistenti = entity.Tipologie.Select(t => t.TipologiaId).ToHashSet();
        foreach (var tipologiaId in richieste.Where(id => !esistenti.Contains(id)))
        {
            entity.Tipologie.Add(new PayTouristStrutturaTipologia { PayTouristStrutturaId = entity.Id, TipologiaId = tipologiaId });
        }
    }
}
