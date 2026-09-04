using GestiSoft.Application.Clienti;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Utenti;

namespace GestiSoft.Application.Auth;

/// <summary>
/// Verifica che l'utente corrente possa operare su una data Struttura: il Super Admin passa
/// sempre, un utente normale solo se la Struttura appartiene al proprio Cliente **e** non è stata
/// disattivata (soft-delete) **e** quel Cliente non è stato sospeso dal Super Admin **e** lui
/// stesso non è stato disattivato **e** la licenza software GestiSoft della Struttura
/// (<see cref="Domain.Entities.Struttura.ScadenzaLicenza"/>, NON la licenza Wubook — quella è solo
/// il Codice struttura/lcode) non è scaduta — su richiesta esplicita, questa scadenza funge da leva
/// di pagamento sull'intera Struttura, indipendente da quali integrazioni esterne siano concesse; non
/// blocca però il login in sé, solo l'uso della Struttura, così l'utente riesce comunque ad
/// autenticarsi e a vedere quale Struttura va rinnovata. Punto centrale da riusare in tutti i servizi
/// applicativi che ricevono uno StrutturaId dall'esterno, così il controllo multi-tenant non viene
/// duplicato/dimenticato modulo per modulo — un token già emesso prima della sospensione/
/// disattivazione/scadenza smette di funzionare qui, non solo a un nuovo login.
/// Un utente normale (non titolare) deve inoltre avere un'assegnazione UtenteStruttura esplicita su
/// quella Struttura: appartenere al Cliente non basta più, solo il titolare (Utente.IsClienteAccount)
/// ha libero accesso a tutte le Strutture del proprio Cliente senza bisogno di assegnazioni.
/// </summary>
public class TenantAccessGuard(
    IStrutturaRepository strutture,
    IClienteRepository clienti,
    IUtenteRepository utenti,
    IUtenteStrutturaRepository utentiStrutture)
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

        if (!utente.IsClienteAccount)
        {
            var assegnazione = await utentiStrutture.GetAsync(currentUser.UtenteId, strutturaId, cancellationToken);
            if (assegnazione is null)
            {
                throw new ForbiddenException("Non sei assegnato a questa struttura.");
            }
        }

        if (struttura.ScadenzaLicenza is { } scadenza && scadenza < DateTime.UtcNow)
        {
            throw new ForbiddenException("Licenza scaduta per questa struttura. Contatta l'assistenza GestiSoft per rinnovarla.");
        }
    }
}
