using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Utenti;
using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Strutture;

public record CreaStrutturaRequest(Guid ClienteId, string Nome);

public record AggiornaStrutturaRequest(string Nome);

/// <param name="RinnovoPagato">
/// True solo se il Super Admin ha confermato esplicitamente che questa modifica alla scadenza
/// corrisponde a un pagamento reale del Cliente (chiesto in UI solo quando la scadenza cambia
/// davvero — un rinnovo può anche essere gratuito, es. una correzione). Se true, registra una riga
/// in RinnovoLicenza per alimentare gli incassi della pagina Statistiche Super Admin.
/// </param>
public record AggiornaLicenzaStrutturaRequest(DateTime? ScadenzaLicenza, bool RinnovoPagato = false, decimal? ImportoRinnovo = null);

public class StrutturaService(
    IStrutturaRepository repository,
    IUtenteRepository utenti,
    IRinnovoLicenzaRepository rinnoviLicenza)
{
    /// <summary>
    /// Il Super Admin vede tutte le Strutture (anche disattivate o a licenza scaduta, o filtrate per
    /// un Cliente a scelta), sempre e comunque — deve poterci entrare per rinnovarle. Il titolare del
    /// Cliente (Utente.IsClienteAccount) è il "proprietario" delle Strutture: vede anche lui tutte
    /// quelle attive del proprio Cliente, comprese quelle con la licenza scaduta (mostrate disabilitate
    /// con "Da rinnovare" in UI, così sa cosa sollecitare a GestiSoft) — è l'unico, oltre al Super
    /// Admin, autorizzato a saperlo. Un utente normale (anche con ruolo Administrator su quella
    /// specifica Struttura) non deve invece vedere per niente una Struttura a licenza scaduta, nemmeno
    /// tra quelle a cui è assegnato: non è un problema suo, è tra GestiSoft e il Cliente — se è
    /// assegnato a più Strutture e una è scaduta, continua a vedere solo le altre.
    /// </summary>
    public async Task<IReadOnlyList<Struttura>> ListAsync(ICurrentUser currentUser, Guid? filtroClienteId, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return await repository.ListByClienteAsync(filtroClienteId, includiInattive: true, cancellationToken);
        }

        var utente = await utenti.GetByIdAsync(currentUser.UtenteId, cancellationToken);
        if (utente is { IsClienteAccount: true })
        {
            return await repository.ListByClienteAsync(currentUser.ClienteId, includiInattive: false, cancellationToken);
        }

        var assegnate = await repository.ListAssegnateAsync(currentUser.UtenteId, cancellationToken);
        return assegnate.Where(s => !IsLicenzaScaduta(s)).ToList();
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

        // Nessuna assegnazione UtenteStruttura automatica qui, su richiesta esplicita: un utente può
        // lavorare in una Struttura e non in un'altra, anche dello stesso Cliente — chi deve avere
        // accesso a questa Struttura va assegnato esplicitamente dalla schermata Utenti (o è il
        // titolare del Cliente, Utente.IsClienteAccount, che ha libero accesso a tutte le Strutture
        // del proprio Cliente senza bisogno di alcuna assegnazione, vedi TenantAccessGuard).
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
            var altreAttive = await repository.ListByClienteAsync(struttura.ClienteId, includiInattive: false, cancellationToken);
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

    /// <summary>
    /// Vero solo se la licenza software GestiSoft di questa Struttura ha una scadenza già impostata e
    /// superata — mai per una licenza mai configurata (una Struttura appena creata dal Super Admin
    /// non deve risultare "da rinnovare" prima ancora di essere stata configurata). Indipendente da
    /// quali integrazioni esterne siano concesse: NON è la licenza Wubook. Usato per marcare la
    /// Struttura come "Da rinnovare" nel selettore (TenantAccessGuard applica poi il blocco vero e
    /// proprio sull'uso reale della Struttura).
    /// </summary>
    public static bool IsLicenzaScaduta(Struttura struttura) =>
        struttura.ScadenzaLicenza is { } scadenza && scadenza < DateTime.UtcNow;

    /// <summary>Lettura completa (Super Admin) della licenza software GestiSoft di una Struttura.</summary>
    public async Task<Struttura> GetLicenzaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await repository.GetByIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");
    }

    /// <summary>
    /// Assegna/rinnova la licenza software GestiSoft di una Struttura — solo il Super Admin, mai il
    /// Cliente. Se il Super Admin conferma che è un rinnovo pagato, registra anche l'incasso in
    /// RinnovoLicenza (vedi Statistiche Super Admin).
    /// </summary>
    public async Task<Struttura> AggiornaLicenzaAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaLicenzaStrutturaRequest request, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var struttura = await repository.GetByIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        struttura.ScadenzaLicenza = request.ScadenzaLicenza;
        await repository.UpdateAsync(struttura, cancellationToken);

        if (request.RinnovoPagato && request.ScadenzaLicenza is { } scadenza)
        {
            await rinnoviLicenza.AddAsync(new RinnovoLicenza { StrutturaId = strutturaId, ScadenzaImpostata = scadenza, Importo = request.ImportoRinnovo }, cancellationToken);
        }

        return struttura;
    }

    private static void RichiediSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può gestire la licenza di una struttura.");
        }
    }
}
