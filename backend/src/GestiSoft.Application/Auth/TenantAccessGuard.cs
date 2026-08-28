using GestiSoft.Application.Clienti;
using GestiSoft.Application.Exceptions;

namespace GestiSoft.Application.Auth;

/// <summary>
/// Verifica che l'utente corrente possa operare su una data Struttura: il Super Admin passa
/// sempre, un utente normale solo se la Struttura appartiene al proprio Cliente **e** non è stata
/// disattivata (soft-delete) **e** quel Cliente non è stato sospeso dal Super Admin **e** lui
/// stesso non è stato disattivato. Punto centrale da riusare in tutti i servizi applicativi che
/// ricevono uno StrutturaId dall'esterno, così il controllo multi-tenant (incluso il blocco di una
/// Struttura eliminata, di un Cliente sospeso o di un singolo Utente disattivato) non viene
/// duplicato/dimenticato modulo per modulo — un token già emesso prima della sospensione/
/// disattivazione smette di funzionare qui, non solo a un nuovo login.
/// </summary>
public class TenantAccessGuard(IStrutturaRepository strutture, IClienteRepository clienti, IUtenteRepository utenti)
{
    public async Task EnsureAccessAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return;
        }

        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        if (currentUser.ClienteId != struttura.ClienteId)
        {
            throw new ForbiddenException("Non hai accesso a questa struttura.");
        }

        if (!struttura.Attivo)
        {
            throw new ForbiddenException("Questa struttura è stata eliminata.");
        }

        var cliente = await clienti.GetByIdAsync(struttura.ClienteId, cancellationToken);
        if (cliente is null || !cliente.Attivo)
        {
            throw new ForbiddenException("Il tuo account è stato sospeso. Contatta l'assistenza.");
        }

        var utente = await utenti.GetByIdAsync(currentUser.UtenteId, cancellationToken);
        if (utente is null || !utente.Attivo)
        {
            throw new ForbiddenException("Il tuo utente è stato disattivato. Contatta l'assistenza.");
        }
    }
}
