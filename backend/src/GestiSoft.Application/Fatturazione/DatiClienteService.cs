using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione;

public record CreaDatiClienteRequest(
    string? Iso2,
    string? PIva,
    string? CodiceFiscale,
    string? Denominazione,
    string? Nome,
    string? Cognome,
    string? Indirizzo,
    string? NCivico,
    string? Cap,
    string? LuogoResidenza,
    string? Provincia,
    string? Cittadinanza,
    string? CodiceDestinatario,
    string? Pec);

/// <summary>CRUD manuale dell'anagrafica clienti fatturabili — porta DatiCliente/SearchClientCommand del legacy.</summary>
public class DatiClienteService(IDatiClienteRepository clienti, PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<DatiCliente>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await clienti.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<DatiCliente> CreaAsync(ICurrentUser currentUser, Guid strutturaId, CreaDatiClienteRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = new DatiCliente { StrutturaId = strutturaId };
        Applica(entity, request);

        await clienti.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<DatiCliente> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, Guid clienteId, CreaDatiClienteRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = await clienti.GetAsync(clienteId, cancellationToken) ?? throw new NotFoundException("Cliente non trovato.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Cliente non trovato.");
        }

        Applica(entity, request);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await clienti.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    private static void Applica(DatiCliente entity, CreaDatiClienteRequest request)
    {
        entity.Iso2 = request.Iso2;
        entity.PIva = request.PIva;
        entity.CodiceFiscale = request.CodiceFiscale;
        entity.Denominazione = request.Denominazione;
        entity.Nome = request.Nome;
        entity.Cognome = request.Cognome;
        entity.Indirizzo = request.Indirizzo;
        entity.NCivico = request.NCivico;
        entity.Cap = request.Cap;
        entity.LuogoResidenza = request.LuogoResidenza;
        entity.Provincia = request.Provincia;
        entity.Cittadinanza = request.Cittadinanza;
        entity.CodiceDestinatario = request.CodiceDestinatario;
        entity.Pec = request.Pec;
    }
}
