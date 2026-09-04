using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Utenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Strutture;

public record CreaStrutturaRequest(Guid ClienteId, string Nome);

public record AggiornaStrutturaRequest(string Nome);

public class StrutturaService(IStrutturaRepository repository, IUtenteStrutturaRepository utentiStrutture, IUtenteRepository utenti)
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

    /// <summary>
    /// Solo il Super Admin crea Strutture, mai il Cliente per sé stesso (stessa regola di
    /// ClienteService per i Clienti): sono l'unità su cui GestiSoft attiva/fattura il servizio, non
    /// qualcosa da attivare in autonomia.
    /// </summary>
    public async Task<Struttura> CreaAsync(ICurrentUser currentUser, CreaStrutturaRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può creare una nuova struttura. Contatta l'assistenza GestiSoft.");
        }

        var struttura = new Struttura
        {
            ClienteId = request.ClienteId,
            Nome = request.Nome,
        };

        // I 4 flag di concessione servizi restano al default (false, vedi Struttura.WubookAbilitato
        // e commento lì): una Struttura nuova non deve avere nulla di attivo finché il Super Admin
        // non lo concede esplicitamente, anche se il Cliente ne ha già altre abilitate.
        await repository.AddAsync(struttura, cancellationToken);

        // Il Super Admin non è un utente del Cliente: senza questo, la Struttura appena creata
        // resterebbe inaccessibile a chiunque dal lato Cliente (PermessoStrutturaGuard/
        // GestioneUtentiGuard richiedono sempre una UtenteStruttura, mai solo l'appartenenza al
        // Cliente proprietario) — un problema reale soprattutto per la primissima Struttura di un
        // Cliente nuovo, dove nessuno avrebbe altrimenti un modo di assegnarsela dalla schermata
        // Utenti. Assegnato automaticamente come Administrator a tutti gli utenti già esistenti di
        // quel Cliente, non solo al primo/admin.
        var utentiCliente = await utenti.ListByClienteIdAsync(request.ClienteId, cancellationToken);
        foreach (var utente in utentiCliente)
        {
            await utentiStrutture.UpsertAsync(
                new UtenteStruttura
                {
                    UtenteId = utente.Id,
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
        struttura.DisattivataAtUtc = attivo ? null : DateTime.UtcNow;
        await repository.UpdateAsync(struttura, cancellationToken);
        return struttura;
    }
}
