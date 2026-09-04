using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Utenti;

namespace GestiSoft.Application.Auth;

/// <summary>
/// Verifica che l'utente corrente possa gestire gli utenti di una Struttura (permesso SettingUser)
/// — porta d'accesso comune a tutta la sezione "Amministrazione" del frontend (Utenti/Impostazioni/
/// Log, vedi navItems.ts): un lavoratore normale non deve poterle interrogare nemmeno via Api
/// diretta, non solo non vederle nel menu (richiesta esplicita). Il Super Admin ha sempre accesso,
/// non ha una riga UtenteStruttura; il titolare del Cliente (Utente.IsClienteAccount) allo stesso
/// modo, avendo libero accesso a tutte le Strutture del proprio Cliente senza bisogno di permessi
/// granulari.
/// </summary>
public class GestioneUtentiGuard(IUtenteStrutturaRepository utentiStrutture, IUtenteRepository utenti)
{
    public async Task EnsureAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            return;
        }

        var utente = await utenti.GetByIdAsync(currentUser.UtenteId, cancellationToken);
        if (utente is { IsClienteAccount: true })
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
