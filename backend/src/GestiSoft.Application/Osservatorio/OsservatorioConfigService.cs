using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Osservatorio;

public record SalvaOsservatorioAppartamentoRequest(
    string? Nome,
    string? EntityCode,
    string? Password,
    string? HotelCode,
    IReadOnlyList<Guid> TipologieIds);

/// <summary>
/// Configurazione degli Appartamenti (entità PMS) Osservatorio Turistico per Struttura — una
/// Struttura può averne più di uno (ognuno instrada un sottoinsieme di tipologie camera, porta
/// GestiCache.Apartments del legacy). Nessun flag di permesso dedicato "Osservatorio" nel modello
/// UtenteStruttura: riusa <c>StatePoliceSettings</c>, stesso riuso pragmatico già fatto per
/// Alloggiati Web in Fase 6 e per Wubook in Fase 5.
/// </summary>
public class OsservatorioConfigService(
    IOsservatorioAppartamentoRepository repository,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<OsservatorioAppartamento>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);
        return await repository.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<OsservatorioAppartamento> CreaAsync(ICurrentUser currentUser, Guid strutturaId, SalvaOsservatorioAppartamentoRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = new OsservatorioAppartamento { StrutturaId = strutturaId };
        Applica(entity, request);

        await repository.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<OsservatorioAppartamento> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, Guid appartamentoId, SalvaOsservatorioAppartamentoRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await repository.GetAsync(strutturaId, appartamentoId, cancellationToken)
            ?? throw new NotFoundException("Appartamento Osservatorio Turistico non trovato.");

        Applica(entity, request);

        await repository.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    private void Applica(OsservatorioAppartamento entity, SalvaOsservatorioAppartamentoRequest request)
    {
        entity.Nome = request.Nome;
        entity.EntityCode = request.EntityCode;
        entity.Password = request.Password;
        entity.HotelCode = request.HotelCode;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        SincronizzaTipologie(entity, request.TipologieIds);
    }

    private void SincronizzaTipologie(OsservatorioAppartamento entity, IReadOnlyList<Guid> tipologieIds)
    {
        var richieste = tipologieIds.ToHashSet();

        foreach (var daRimuovere in entity.Tipologie.Where(t => !richieste.Contains(t.TipologiaId)).ToList())
        {
            entity.Tipologie.Remove(daRimuovere);
            repository.RimuoviTipologia(daRimuovere);
        }

        var esistenti = entity.Tipologie.Select(t => t.TipologiaId).ToHashSet();
        foreach (var tipologiaId in richieste.Where(id => !esistenti.Contains(id)))
        {
            entity.Tipologie.Add(new OsservatorioAppartamentoTipologia { OsservatorioAppartamentoId = entity.Id, TipologiaId = tipologiaId });
        }
    }
}
