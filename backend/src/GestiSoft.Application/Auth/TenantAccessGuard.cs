using GestiSoft.Application.Exceptions;

namespace GestiSoft.Application.Auth;

/// <summary>
/// Verifica che l'utente corrente possa operare su una data Struttura: il Super Admin passa
/// sempre, un utente normale solo se la Struttura appartiene al proprio Cliente. Punto centrale
/// da riusare in tutti i servizi applicativi che ricevono uno StrutturaId dall'esterno, così il
/// controllo multi-tenant non viene duplicato/dimenticato modulo per modulo.
/// </summary>
public class TenantAccessGuard(IStrutturaRepository strutture)
{
    public async Task EnsureAccessAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return;
        }

        var clienteId = await strutture.GetClienteIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        if (currentUser.ClienteId != clienteId)
        {
            throw new ForbiddenException("Non hai accesso a questa struttura.");
        }
    }
}
