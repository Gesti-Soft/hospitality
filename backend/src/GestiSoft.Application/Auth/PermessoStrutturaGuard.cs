using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Utenti;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Auth;

/// <summary>
/// Estende TenantAccessGuard con la verifica dei permessi granulari per-modulo di
/// UtenteStruttura (BookingRead/Write, ReservationRead/Write, SettingRoomRead/Write, ecc.).
/// Il Super Admin bypassa sempre, come TenantAccessGuard; un utente normale deve avere
/// un'assegnazione UtenteStruttura su quella Struttura con il flag richiesto attivo — non basta
/// più appartenere al Cliente proprietario. Punto centrale da riusare in ogni servizio dei moduli
/// operativi (Camere, Prenotazioni, Ospiti, ...) al posto della sola TenantAccessGuard.
/// </summary>
public class PermessoStrutturaGuard(TenantAccessGuard accessGuard, IUtenteStrutturaRepository utentiStrutture)
{
    public async Task EnsureAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        Func<UtenteStruttura, bool> haPermesso,
        CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);

        if (currentUser.IsSuperAdmin)
        {
            return;
        }

        var assegnazione = await utentiStrutture.GetAsync(currentUser.UtenteId, strutturaId, cancellationToken);
        if (assegnazione is null || !haPermesso(assegnazione))
        {
            throw new ForbiddenException("Non hai i permessi necessari per questa operazione su questa struttura.");
        }
    }
}
