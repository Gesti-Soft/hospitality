using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Utenti;

namespace GestiSoft.Application.Auth;

/// <summary>
/// Verifica che l'utente corrente possa gestire gli utenti di una Struttura (permesso SettingUser)
/// — porta d'accesso comune a tutta la sezione "Amministrazione" del frontend (Utenti/Impostazioni/
/// Log, vedi navItems.ts): un lavoratore normale non deve poterle interrogare nemmeno via Api
/// diretta, non solo non vederle nel menu (richiesta esplicita). Il Super Admin ha sempre accesso,
/// non ha una riga UtenteStruttura.
/// </summary>
public class GestioneUtentiGuard(IUtenteStrutturaRepository utentiStrutture)
{
    public async Task EnsureAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return;
        }

        var assegnazione = await utentiStrutture.GetAsync(currentUser.UtenteId, strutturaId, cancellationToken);
        if (assegnazione?.SettingUser != true)
        {
            throw new ForbiddenException("Solo chi gestisce gli utenti di questa struttura può eseguire questa azione.");
        }
    }
}
