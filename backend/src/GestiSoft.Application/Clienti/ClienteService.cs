using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Clienti;

public record CreaClienteRequest(string RagioneSociale, string? PartitaIva);

public record AggiornaClienteRequest(string RagioneSociale, string? PartitaIva);

/// <summary>Solo il Super Admin gestisce i Clienti: sono la radice dei tenant, non qualcosa che un Cliente crea per sé stesso.</summary>
public class ClienteService(IClienteRepository repository)
{
    public async Task<IReadOnlyList<Cliente>> ListAsync(ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await repository.ListAsync(cancellationToken);
    }

    public async Task<Cliente> GetByIdAsync(ICurrentUser currentUser, Guid id, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Cliente non trovato.");
    }

    public async Task<Cliente> CreaAsync(ICurrentUser currentUser, CreaClienteRequest request, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var cliente = new Cliente
        {
            RagioneSociale = request.RagioneSociale,
            PartitaIva = request.PartitaIva,
        };

        await repository.AddAsync(cliente, cancellationToken);
        return cliente;
    }

    public async Task<Cliente> AggiornaAsync(ICurrentUser currentUser, Guid id, AggiornaClienteRequest request, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var cliente = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Cliente non trovato.");

        cliente.RagioneSociale = request.RagioneSociale;
        cliente.PartitaIva = request.PartitaIva;

        await repository.UpdateAsync(cliente, cancellationToken);
        return cliente;
    }

    private static void RichiediSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può gestire i Clienti.");
        }
    }
}
