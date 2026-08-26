using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Strutture;

public record CreaStrutturaRequest(Guid? ClienteId, string Nome);

public class StrutturaService(IStrutturaRepository repository)
{
    /// <summary>SuperAdmin vede tutte le Strutture (o filtrate per un Cliente a scelta); un Cliente vede solo le proprie.</summary>
    public async Task<IReadOnlyList<Struttura>> ListAsync(ICurrentUser currentUser, Guid? filtroClienteId, CancellationToken cancellationToken)
    {
        var clienteId = currentUser.IsSuperAdmin ? filtroClienteId : currentUser.ClienteId;
        return await repository.ListByClienteAsync(clienteId, cancellationToken);
    }

    public async Task<Struttura> GetByIdAsync(ICurrentUser currentUser, Guid id, CancellationToken cancellationToken)
    {
        var struttura = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        if (!currentUser.IsSuperAdmin && struttura.ClienteId != currentUser.ClienteId)
        {
            throw new ForbiddenException("Non hai accesso a questa struttura.");
        }

        return struttura;
    }

    public async Task<Struttura> CreaAsync(ICurrentUser currentUser, CreaStrutturaRequest request, CancellationToken cancellationToken)
    {
        // Un Cliente crea Strutture solo per sé stesso; solo il Super Admin può specificare
        // un ClienteId diverso (es. per creare la prima struttura di un nuovo cliente).
        var clienteId = currentUser.IsSuperAdmin
            ? request.ClienteId ?? throw new ConflictException("Specificare il Cliente proprietario della struttura.")
            : currentUser.ClienteId ?? throw new ForbiddenException("Utente non associato a nessun Cliente.");

        var struttura = new Struttura
        {
            ClienteId = clienteId,
            Nome = request.Nome,
        };

        await repository.AddAsync(struttura, cancellationToken);
        return struttura;
    }
}
