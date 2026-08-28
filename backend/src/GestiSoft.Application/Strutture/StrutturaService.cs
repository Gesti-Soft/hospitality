using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Utenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Strutture;

public record CreaStrutturaRequest(Guid? ClienteId, string Nome);

public record AggiornaStrutturaRequest(string Nome);

public class StrutturaService(IStrutturaRepository repository, IUtenteStrutturaRepository utentiStrutture)
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

        // I 4 flag di concessione servizi restano al default (false, vedi Struttura.WubookAbilitato
        // e commento lì): una Struttura nuova non deve avere nulla di attivo finché il Super Admin
        // non lo concede esplicitamente, anche se il Cliente ne ha già altre abilitate.
        await repository.AddAsync(struttura, cancellationToken);

        // Chi crea la Struttura per sé stesso deve poterla usare subito: senza questa assegnazione
        // resterebbe senza alcun permesso su una Struttura che ha appena creato (PermessoStrutturaGuard
        // richiede sempre una UtenteStruttura, appartenere al Cliente proprietario non basta). Un
        // Super Admin che crea una Struttura per conto di un Cliente non riceve un'assegnazione: non
        // è un utente di quel Cliente, sarà il Cliente ad assegnare i ruoli dalla schermata Utenti.
        if (!currentUser.IsSuperAdmin)
        {
            await utentiStrutture.UpsertAsync(
                new UtenteStruttura
                {
                    UtenteId = currentUser.UtenteId,
                    StrutturaId = struttura.Id,
                    Ruolo = RuoloUtente.Administrator,
                    BookingRead = true,
                    BookingWrite = true,
                    ReservationRead = true,
                    ReservationWrite = true,
                    StatePoliceRead = true,
                    StatePoliceWrite = true,
                    StatePoliceSettings = true,
                    SettingAgency = true,
                    SettingUser = true,
                    SettingRoomRead = true,
                    SettingRoomWrite = true,
                    RoomStatusUpdate = true,
                    FinanceRead = true,
                    FinanceWrite = true,
                    RestaurantRead = true,
                    RestaurantWrite = true,
                },
                cancellationToken);
        }

        return struttura;
    }

    public async Task<Struttura> AggiornaAsync(ICurrentUser currentUser, Guid id, AggiornaStrutturaRequest request, CancellationToken cancellationToken)
    {
        var struttura = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        if (!currentUser.IsSuperAdmin && struttura.ClienteId != currentUser.ClienteId)
        {
            throw new ForbiddenException("Non hai accesso a questa struttura.");
        }

        struttura.Nome = request.Nome;
        await repository.UpdateAsync(struttura, cancellationToken);
        return struttura;
    }

    /// <summary>
    /// "Eliminazione"/riattivazione di una Struttura: in realtà un soft-delete (Struttura.Attivo),
    /// mai una vera cancellazione — i dati collegati (camere, prenotazioni, ospiti, fatture...)
    /// restano intatti. Bloccata la disattivazione dell'ultima Struttura attiva di un Cliente: il
    /// gestionale richiede sempre almeno una Struttura utilizzabile, altrimenti l'utente si
    /// ritroverebbe rispedito alla schermata di onboarding.
    /// </summary>
    public async Task<Struttura> ImpostaAttivoAsync(ICurrentUser currentUser, Guid id, bool attivo, CancellationToken cancellationToken)
    {
        var struttura = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        if (!currentUser.IsSuperAdmin && struttura.ClienteId != currentUser.ClienteId)
        {
            throw new ForbiddenException("Non hai accesso a questa struttura.");
        }

        if (!attivo)
        {
            var altreAttive = await repository.ListByClienteAsync(struttura.ClienteId, cancellationToken);
            if (altreAttive.All(s => s.Id == struttura.Id))
            {
                throw new ConflictException("Non puoi eliminare l'unica struttura rimasta: il gestionale richiede almeno una struttura attiva.");
            }
        }

        struttura.Attivo = attivo;
        await repository.UpdateAsync(struttura, cancellationToken);
        return struttura;
    }
}
