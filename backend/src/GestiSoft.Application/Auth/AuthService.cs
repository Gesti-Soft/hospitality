using GestiSoft.Application.Clienti;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Utenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace GestiSoft.Application.Auth;

public record LoginResult(string Token, DateTime ScadeAtUtc, Guid UtenteId, string Email, bool IsSuperAdmin, Guid? ClienteId, bool IsClienteAccount);

public class AuthService(
    IUtenteRepository utenti,
    IClienteRepository clienti,
    IUtenteStrutturaRepository utentiStrutture,
    IStrutturaRepository strutture,
    IPasswordHasher<Utente> passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ILogEventoService logEventi)
{
    // Messaggio generico riservato al rinnovo silenzioso (RefreshAsync) — lì non è mai mostrato in
    // un form, l'utente scopre la sessione scaduta solo al prossimo 401 su un'azione reale.
    private const string CredenzialiNonValideMessage = "Sessione non valida.";

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var emailNormalizzata = email.Trim().ToLowerInvariant();
        var utente = await utenti.GetByEmailAsync(emailNormalizzata, cancellationToken);

        // Messaggi specifici per caso, su richiesta esplicita dell'utente (rinuncia deliberata alla
        // protezione anti-enumerazione che c'era prima — un messaggio unico per ogni causa di
        // fallimento — perché qui conta di più poter distinguere a colpo d'occhio "email sbagliata"
        // da "password sbagliata" da "utente disabilitato"). Categoria "Auth" non è mai visibile al
        // Cliente (solo il Super Admin vede login/logout, vedi LogVisibilita).
        if (utente is null)
        {
            await LogFallitoAsync(emailNormalizzata, null, cancellationToken);
            throw new UnauthorizedAppException("Email non trovata.");
        }

        // Email e password vanno verificate per prime, prima di qualunque controllo di
        // abilitazione: un account disabilitato con la password sbagliata deve vedere "Password
        // errata", non "Utente disabilitato" — altrimenti chi indovina l'email di un account
        // disabilitato lo scopre senza mai azzeccare la password.
        var esito = passwordHasher.VerifyHashedPassword(utente, utente.PasswordHash, password);
        if (esito == PasswordVerificationResult.Failed)
        {
            await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
            throw new UnauthorizedAppException("Password errata.");
        }

        if (!utente.Attivo)
        {
            await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
            throw new UnauthorizedAppException("Utente disabilitato. Contatta l'amministrazione.");
        }

        if (utente.ClienteId is { } clienteId)
        {
            var cliente = await clienti.GetByIdAsync(clienteId, cancellationToken);
            if (cliente is null || !cliente.Attivo)
            {
                await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
                throw new UnauthorizedAppException("Il tuo account è stato sospeso. Contatta l'amministrazione.");
            }
        }

        // Un utente normale vede solo le Strutture a cui è stato assegnato, un titolare (IsClienteAccount)
        // tutte quelle del proprio Cliente: se TUTTE quelle raggiungibili hanno la licenza GestiSoft
        // scaduta, non c'è nulla che possa davvero fare una volta entrato — meglio bloccarlo qui con un
        // messaggio chiaro piuttosto che lasciarlo entrare in un gestionale dove ogni singola pagina
        // finirebbe comunque rifiutata da TenantAccessGuard. Se invece ha anche una sola Struttura
        // ancora valida, il login riesce: la Struttura scaduta resta comunque bloccata (vedi
        // TenantAccessGuard), le altre no.
        if (!utente.IsSuperAdmin && await TutteLeStruttureBloccateAsync(utente, cancellationToken))
        {
            await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
            throw new UnauthorizedAppException("La licenza della tua struttura è scaduta. Contatta l'assistenza GestiSoft per rinnovarla.");
        }

        var token = tokenGenerator.Generate(utente);

        await logEventi.RegistraAsync(
            LivelloLog.Info,
            "Login effettuato.",
            origine: "Auth",
            clienteId: utente.ClienteId,
            categoria: "Auth",
            operatore: utente.Email,
            cancellationToken: cancellationToken);

        return new LoginResult(token.Value, token.ScadeAtUtc, utente.Id, utente.Email, utente.IsSuperAdmin, utente.ClienteId, utente.IsClienteAccount);
    }

    /// <summary>
    /// Rinnova il token di un utente già autenticato (chiamato dal frontend in background mentre
    /// l'utente è attivo) — stessi controlli del login (utente/Cliente attivi) ma senza password,
    /// il chiamante deve già possedere un JWT valido e non scaduto (endpoint protetto da
    /// [Authorize]). Nessun LogEvento qui: a differenza del login vero e proprio, un rinnovo può
    /// avvenire più volte l'ora e non è un evento significativo da mostrare in pagina Log.
    /// </summary>
    public async Task<LoginResult> RefreshAsync(Guid utenteId, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken);
        if (utente is null || !utente.Attivo)
        {
            throw new UnauthorizedAppException(CredenzialiNonValideMessage);
        }

        if (utente.ClienteId is { } clienteId)
        {
            var cliente = await clienti.GetByIdAsync(clienteId, cancellationToken);
            if (cliente is null || !cliente.Attivo)
            {
                throw new UnauthorizedAppException(CredenzialiNonValideMessage);
            }
        }

        if (!utente.IsSuperAdmin && await TutteLeStruttureBloccateAsync(utente, cancellationToken))
        {
            throw new UnauthorizedAppException(CredenzialiNonValideMessage);
        }

        var token = tokenGenerator.Generate(utente);
        return new LoginResult(token.Value, token.ScadeAtUtc, utente.Id, utente.Email, utente.IsSuperAdmin, utente.ClienteId, utente.IsClienteAccount);
    }

    /// <summary>
    /// True solo se l'utente ha almeno una Struttura attiva raggiungibile e TUTTE quelle attive hanno
    /// la licenza GestiSoft scaduta — un'unica Struttura ancora valida basta a far riuscire comunque il
    /// login. Per un titolare (IsClienteAccount) le Strutture raggiungibili sono tutte quelle attive
    /// del proprio Cliente (libero accesso, mai limitato alle sole assegnazioni); per un utente normale
    /// sono solo quelle a cui è stato esplicitamente assegnato in UtenteStruttura. Una Struttura
    /// soft-eliminata non conta né a favore né contro; una licenza mai impostata (null) non conta come
    /// scaduta.
    /// </summary>
    private async Task<bool> TutteLeStruttureBloccateAsync(Utente utente, CancellationToken cancellationToken)
    {
        var struttureRaggiungibili = utente.IsClienteAccount
            ? await strutture.ListByClienteAsync(utente.ClienteId, includiInattive: false, cancellationToken)
            : await StruttureAssegnateAttiveAsync(utente.Id, cancellationToken);

        if (struttureRaggiungibili.Count == 0)
        {
            return false;
        }

        return struttureRaggiungibili.All(s => s.ScadenzaLicenza is { } scadenza && scadenza < DateTime.UtcNow);
    }

    private async Task<IReadOnlyList<Struttura>> StruttureAssegnateAttiveAsync(Guid utenteId, CancellationToken cancellationToken)
    {
        var assegnazioni = await utentiStrutture.ListByUtenteIdAsync(utenteId, cancellationToken);
        var risultato = new List<Struttura>();
        foreach (var assegnazione in assegnazioni)
        {
            var struttura = await strutture.GetByIdAsync(assegnazione.StrutturaId, cancellationToken);
            if (struttura is { Attivo: true })
            {
                risultato.Add(struttura);
            }
        }

        return risultato;
    }

    private Task LogFallitoAsync(string emailTentata, Guid? clienteId, CancellationToken cancellationToken) =>
        logEventi.RegistraAsync(
            LivelloLog.Warning,
            $"Login fallito per '{emailTentata}'.",
            origine: "Auth",
            clienteId: clienteId,
            categoria: "Auth",
            cancellationToken: cancellationToken);
}
