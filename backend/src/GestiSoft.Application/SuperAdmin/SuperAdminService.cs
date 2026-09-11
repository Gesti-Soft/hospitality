using GestiSoft.Application.Auth;
using GestiSoft.Application.Clienti;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace GestiSoft.Application.SuperAdmin;

public record ServiziStrutturaRequest(bool WubookAbilitato, bool AlloggiatiWebAbilitato, bool OsservatorioAbilitato, bool PayTouristAbilitato);

public record ResettaPasswordRequest(string NuovaPassword);

public record AggiornaUtenteRequest(string Email, string? Nome, string? Cognome, bool IsClienteAccount);

/// <summary>
/// Vista d'insieme cross-Cliente per il Super Admin (staff GestiSoft): Clienti/Strutture/Utenti,
/// stato delle integrazioni esterne, e le "leve" di gestione discusse esplicitamente con l'utente
/// (sospensione Cliente, concessione servizi esterni per Cliente, reset password di supporto) —
/// finora tutte assenti dall'interfaccia, solo raggiungibili via API diretta o mai applicate a
/// runtime (vedi Cliente.Attivo, mai controllato prima d'ora).
/// </summary>
public class SuperAdminService(
    ISuperAdminRepository repository,
    IClienteRepository clienti,
    IStrutturaRepository strutture,
    IUtenteRepository utenti,
    IPasswordHasher<Utente> passwordHasher,
    IDueFattoriRepository dueFattori,
    ILogEventoService logEventi,
    IBackupExporter backupExporter)
{
    public async Task<DashboardSuperAdminInfo> GetDashboardAsync(ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await repository.GetDashboardAsync(cancellationToken);
    }

    /// <summary>
    /// Dump completo del database generato al volo per il download manuale — indipendente dal
    /// sistema di backup automatico notturno (vedi docs/backup-restore.md).
    /// </summary>
    public async Task<byte[]> EsportaBackupAsync(ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        var dump = await backupExporter.EsportaAsync(cancellationToken);

        // È l'unica operazione che porta fuori dal sistema una copia completa del database: va
        // sempre tracciata con chi l'ha fatta e quando, anche quando va a buon fine. Categoria
        // "Backup" come i backup notturni (stessa pagina Super Admin > Backup), ma origine "Api" e
        // Operatore valorizzato: è quello che distingue a colpo d'occhio un download manuale dal
        // backup automatico, che di operatore non ne ha.
        await logEventi.RegistraAsync(
            LivelloLog.Info,
            $"Backup del database scaricato manualmente ({FormattaDimensione(dump.LongLength)}).",
            origine: "Api",
            categoria: "Backup",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

        return dump;
    }

    private static string FormattaDimensione(long byteTotali) =>
        byteTotali >= 1024L * 1024L
            ? $"{byteTotali / 1024d / 1024d:N1} MB"
            : $"{byteTotali / 1024d:N0} KB";

    public async Task<Cliente> ImpostaAttivoAsync(ICurrentUser currentUser, Guid clienteId, bool attivo, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var cliente = await clienti.GetByIdAsync(clienteId, cancellationToken)
            ?? throw new NotFoundException("Cliente non trovato.");

        cliente.Attivo = attivo;
        await clienti.UpdateAsync(cliente, cancellationToken);

        // Categoria "SuperAdmin": mai visibile al Cliente (vedi LogVisibilita) — sospendere/riattivare
        // l'accesso di un intero Cliente è una leva del Super Admin, non un evento operativo suo.
        await logEventi.RegistraAsync(
            LivelloLog.Info,
            $"Cliente {(attivo ? "riattivato" : "sospeso")}.",
            origine: "SuperAdmin",
            clienteId: clienteId,
            categoria: "SuperAdmin",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

        return cliente;
    }

    /// <summary>
    /// Concessione dei servizi esterni per singola Struttura (non più per l'intero Cliente): due
    /// Strutture dello stesso Cliente possono avere concessioni diverse.
    /// </summary>
    public async Task<Domain.Entities.Struttura> AggiornaServiziAsync(ICurrentUser currentUser, Guid strutturaId, ServiziStrutturaRequest request, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        // Un log per ogni servizio che cambia davvero stato (non uno generico "servizi aggiornati"):
        // categoria "Servizi", visibile anche al Cliente (vedi LogVisibilita) — deve sapere quando e
        // perché un'integrazione smette/inizia a funzionare per la sua struttura.
        var cambi = new (string Nome, bool Prima, bool Dopo)[]
        {
            ("Wubook", struttura.WubookAbilitato, request.WubookAbilitato),
            ("Alloggiati Web", struttura.AlloggiatiWebAbilitato, request.AlloggiatiWebAbilitato),
            ("Osservatorio Turistico", struttura.OsservatorioAbilitato, request.OsservatorioAbilitato),
            ("PayTourist", struttura.PayTouristAbilitato, request.PayTouristAbilitato),
        };

        struttura.WubookAbilitato = request.WubookAbilitato;
        struttura.AlloggiatiWebAbilitato = request.AlloggiatiWebAbilitato;
        struttura.OsservatorioAbilitato = request.OsservatorioAbilitato;
        struttura.PayTouristAbilitato = request.PayTouristAbilitato;

        await strutture.UpdateAsync(struttura, cancellationToken);

        foreach (var (nome, prima, dopo) in cambi)
        {
            if (prima == dopo)
            {
                continue;
            }

            await logEventi.RegistraAsync(
                LivelloLog.Info,
                $"Servizio {nome} {(dopo ? "attivato" : "disattivato")} per questa struttura.",
                origine: "SuperAdmin",
                clienteId: struttura.ClienteId,
                strutturaId: strutturaId,
                categoria: "Servizi",
                operatore: currentUser.Email,
                cancellationToken: cancellationToken);
        }

        return struttura;
    }

    /// <summary>
    /// Reset password di supporto/assistenza: il Super Admin imposta una nuova password per conto
    /// del Cliente senza dover conoscere quella attuale (a differenza di CambiaPasswordAsync, che
    /// un utente usa su se stesso e richiede la password attuale).
    /// </summary>
    public async Task ResettaPasswordAsync(ICurrentUser currentUser, Guid utenteId, ResettaPasswordRequest request, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        utente.PasswordHash = passwordHasher.HashPassword(utente, request.NuovaPassword);
        // Chi chiede il reset ha quasi sempre appena finito i tentativi sbagliando la password che
        // non ricordava: lasciargli il blocco addosso vorrebbe dire dargli una password nuova che
        // non funziona per un quarto d'ora, proprio nel momento in cui ha fretta.
        utente.TentativiLoginFalliti = 0;
        utente.BloccatoFinoUtc = null;
        await utenti.UpdateAsync(utente, cancellationToken);
        await LogUtenteAsync(currentUser, utente, $"Password reimpostata per {utente.Email} (supporto Super Admin).", cancellationToken);
    }

    /// <summary>
    /// Toglie il blocco per troppi tentativi senza toccare la password — per chi la password se la
    /// ricorda e si è bloccato per un maiuscolo di troppo: cambiargliela sarebbe una complicazione
    /// inutile. Non fallisce se l'utente non era bloccato: è un'azione di assistenza, chi la usa
    /// vuole solo la certezza che quell'account possa entrare adesso.
    /// </summary>
    public async Task SbloccaAccessoAsync(ICurrentUser currentUser, Guid utenteId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (utente.TentativiLoginFalliti == 0 && utente.BloccatoFinoUtc is null)
        {
            return;
        }

        utente.TentativiLoginFalliti = 0;
        utente.BloccatoFinoUtc = null;
        await utenti.UpdateAsync(utente, cancellationToken);
        await LogUtenteAsync(currentUser, utente, $"Blocco per tentativi falliti rimosso per {utente.Email} (supporto Super Admin).", cancellationToken);
    }

    /// <summary>
    /// Spegne la verifica in due passaggi di un utente che non riesce più ad accedere (telefono
    /// perso insieme ai codici di recupero) — l'unico modo per rimetterlo dentro, dato che il
    /// segreto sta solo nel suo telefono e i codici sono conservati in hash: nessuno, Super Admin
    /// compreso, può rileggerli o rigenerarli al posto suo.
    ///
    /// Disattiva invece di rigenerare di proposito: così il Super Admin non entra mai in possesso
    /// di un secondo fattore altrui. L'utente rientra con la sola password e riconfigura da capo.
    /// Va da sé che prima di premerlo bisogna essere sicuri di chi sta chiedendo: da qui in avanti,
    /// per quell'account, la password torna a bastare.
    /// </summary>
    public async Task ResettaDueFattoriAsync(ICurrentUser currentUser, Guid utenteId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (!utente.TotpAttivo)
        {
            throw new ConflictException("Questo utente non ha la verifica in due passaggi attiva.");
        }

        utente.TotpAttivo = false;
        utente.TotpSecret = null;
        utente.TotpAttivatoAtUtc = null;
        // Anche i tentativi falliti: chi ha provato e riprovato a entrare senza codice si ritrova
        // spesso pure l'account bloccato, e sbloccarlo è parte della stessa richiesta di supporto.
        utente.TentativiLoginFalliti = 0;
        utente.BloccatoFinoUtc = null;
        await utenti.UpdateAsync(utente, cancellationToken);

        await dueFattori.SostituisciCodiciAsync(utente.Id, [], cancellationToken);
        await dueFattori.RimuoviDispositiviAsync(utente.Id, cancellationToken);

        await LogUtenteAsync(
            currentUser,
            utente,
            $"Verifica in due passaggi azzerata per {utente.Email} (supporto Super Admin): l'accesso torna alla sola password.",
            cancellationToken);
    }

    /// <summary>
    /// Modifica i dati anagrafici di base di un Utente (email/nome/cognome) — non tocca Cliente,
    /// ruolo Super Admin o assegnazioni Struttura, che restano decisi alla creazione/tramite
    /// AssegnaRuoloAsync per evitare di orfanizzare permessi legati a un Cliente diverso.
    /// </summary>
    public async Task<Utente> AggiornaUtenteAsync(ICurrentUser currentUser, Guid utenteId, AggiornaUtenteRequest request, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (email != utente.Email)
        {
            if (await utenti.GetByEmailAsync(email, cancellationToken) is not null)
            {
                throw new ConflictException("Esiste già un utente con questa email.");
            }

            utente.Email = email;
        }

        utente.Nome = request.Nome;
        utente.Cognome = request.Cognome;
        utente.IsClienteAccount = request.IsClienteAccount;

        await utenti.UpdateAsync(utente, cancellationToken);
        await LogUtenteAsync(currentUser, utente, $"Profilo utente aggiornato ({utente.Email}) da Super Admin.", cancellationToken);
        return utente;
    }

    /// <summary>
    /// Attiva/disattiva un Utente: a differenza della sospensione di un Cliente (blocca tutti i suoi
    /// utenti insieme), qui si disattiva un singolo account — es. un dipendente che ha lasciato la
    /// struttura, senza sospendere l'intero Cliente. Bloccata l'auto-disattivazione per evitare che
    /// il Super Admin si chiuda fuori da solo.
    /// </summary>
    public async Task<Utente> ImpostaAttivoUtenteAsync(ICurrentUser currentUser, Guid utenteId, bool attivo, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        if (!attivo && utenteId == currentUser.UtenteId)
        {
            throw new ConflictException("Non puoi disattivare il tuo stesso account.");
        }

        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        utente.Attivo = attivo;
        await utenti.UpdateAsync(utente, cancellationToken);
        await LogUtenteAsync(currentUser, utente, $"Utente {utente.Email} {(attivo ? "riattivato" : "disattivato")} da Super Admin.", cancellationToken);
        return utente;
    }

    /// <summary>
    /// Eliminazione DEFINITIVA (hard delete, non il soft-delete di Struttura.Attivo) di una Struttura
    /// e di tutti i suoi dati collegati — irreversibile. Consentita solo per Strutture già disattivate
    /// da almeno 90 giorni (controllo ripetuto qui lato server, non solo filtrato in UI, perché
    /// l'azione non si può annullare). Nessuna cancellazione automatica: va sempre confermata
    /// esplicitamente riga per riga dal Super Admin (vedi ISuperAdminRepository.EliminaStrutturaAsync
    /// per l'ordine di cancellazione delle tabelle collegate).
    /// </summary>
    public async Task EliminaStrutturaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        if (struttura.Attivo || struttura.DisattivataAtUtc is null)
        {
            throw new ConflictException("Solo una struttura disattivata può essere eliminata definitivamente.");
        }

        if (struttura.DisattivataAtUtc.Value > DateTime.UtcNow.AddDays(-90))
        {
            throw new ConflictException("La struttura può essere eliminata definitivamente solo dopo 90 giorni dalla disattivazione.");
        }

        var clienteId = struttura.ClienteId;
        var nomeStruttura = struttura.Nome;
        await repository.EliminaStrutturaAsync(strutturaId, cancellationToken);

        // Loggato PRIMA della richiesta di eliminazione sarebbe scorretto (potrebbe fallire) — qui
        // dopo, ma con Id/nome già letti perché la riga Struttura non esiste più. Categoria
        // "SuperAdmin": azione irreversibile, mai visibile al Cliente.
        await logEventi.RegistraAsync(
            LivelloLog.Warning,
            $"Struttura '{nomeStruttura}' eliminata definitivamente.",
            origine: "SuperAdmin",
            clienteId: clienteId,
            categoria: "SuperAdmin",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Ogni metodo che chiama questo helper è già dietro RichiediSuperAdmin, quindi categoria
    /// "SuperAdmin" sempre — mai "Utente" (quella è riservata alle stesse azioni quando è
    /// davvero il Cliente ad agire sui propri utenti, vedi UtenteManagementService.LogUtenteAsync).
    /// "Tutto quello che fa l'admin il cliente non deve vederlo", anche quando agisce per suo
    /// conto (reset password di supporto, ecc.) — richiesta esplicita dell'utente.
    /// </summary>
    private Task LogUtenteAsync(ICurrentUser currentUser, Utente utenteTarget, string messaggio, CancellationToken cancellationToken) =>
        logEventi.RegistraAsync(
            LivelloLog.Info,
            messaggio,
            origine: "SuperAdmin",
            clienteId: utenteTarget.ClienteId,
            categoria: "SuperAdmin",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

    private static void RichiediSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può accedere a questa dashboard.");
        }
    }
}
