using System.Security.Cryptography;
using System.Text;
using GestiSoft.Application.Clienti;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Utenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace GestiSoft.Application.Auth;

public record LoginResult(string Token, DateTime ScadeAtUtc, Guid UtenteId, string Email, bool IsSuperAdmin, Guid? ClienteId, bool IsClienteAccount);

/// <summary>
/// Esito del primo passo del login. O la sessione è già pronta (nessun 2FA, oppure browser già
/// ricordato), oppure manca solo il codice: in quel caso arriva un <see cref="TokenVerifica2Fa"/>
/// da restituire al secondo passo. Esattamente uno dei due è valorizzato.
/// </summary>
public record EsitoLogin(LoginResult? Sessione, string? TokenVerifica2Fa);

/// <summary>Sessione ottenuta al secondo passo, più il token del browser da ricordare (solo se richiesto).</summary>
public record EsitoVerifica2Fa(LoginResult Sessione, string? TokenDispositivo, DateTime? DispositivoScadeAtUtc, int CodiciRecuperoRimasti);

/// <summary>Dati mostrati una volta sola all'avvio dell'attivazione: il QR da inquadrare e la stessa chiave da digitare a mano se il QR non si legge.</summary>
public record Avvio2Fa(string Secret, string UriOtpauth);

public class AuthService(
    IUtenteRepository utenti,
    IClienteRepository clienti,
    IUtenteStrutturaRepository utentiStrutture,
    IStrutturaRepository strutture,
    IPasswordHasher<Utente> passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ITotpService totp,
    IDueFattoriRepository dueFattori,
    ILogEventoService logEventi)
{
    // Messaggio generico riservato al rinnovo silenzioso (RefreshAsync) — lì non è mai mostrato in
    // un form, l'utente scopre la sessione scaduta solo al prossimo 401 su un'azione reale.
    private const string CredenzialiNonValideMessage = "Sessione non valida.";

    /// <summary>
    /// Password sbagliate di fila prima del blocco temporaneo. Cinque è largo per chi sbaglia a
    /// digitare e strettissimo per chi prova una lista di password: il rate limit generale dell'Api
    /// da solo ne lascerebbe passare una ventina al secondo.
    /// </summary>
    private const int MassimiTentativiLogin = 5;

    /// <summary>Durata del blocco. Si scioglie da solo: vedi Utente.BloccatoFinoUtc per il perché non è mai definitivo.</summary>
    private const int MinutiBlocco = 15;

    /// <summary>Per quanto un browser resta "ricordato" e non chiede più il codice.</summary>
    private const int GiorniDispositivoFidato = 7;

    private const int NumeroCodiciRecupero = 10;

    public async Task<EsitoLogin> LoginAsync(string email, string password, string? tokenDispositivo, CancellationToken cancellationToken)
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
        // Il blocco per troppi tentativi viene prima della verifica della password: è proprio a chi
        // la sta indovinando che non va concessa un'altra prova, e a chi la sa costa solo l'attesa.
        if (BloccoAttivo(utente, out var minutiMancanti))
        {
            await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
            throw new UnauthorizedAppException(
                $"Troppi tentativi falliti: riprova tra {minutiMancanti} minuti.",
                await IndicazioneAssistenzaAsync(utente, cancellationToken));
        }

        var esito = passwordHasher.VerifyHashedPassword(utente, utente.PasswordHash, password);
        if (esito == PasswordVerificationResult.Failed)
        {
            await RegistraTentativoFallitoAsync(utente, cancellationToken);
            await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
            throw new UnauthorizedAppException("Password errata.", await IndicazioneAssistenzaAsync(utente, cancellationToken));
        }

        if (!utente.Attivo)
        {
            await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
            throw new UnauthorizedAppException("Utente disabilitato.", await IndicazioneAssistenzaAsync(utente, cancellationToken));
        }

        if (utente.ClienteId is { } clienteId)
        {
            var cliente = await clienti.GetByIdAsync(clienteId, cancellationToken);
            if (cliente is null || !cliente.Attivo)
            {
                await LogFallitoAsync(emailNormalizzata, utente.ClienteId, cancellationToken);
                throw new UnauthorizedAppException(
                    "Il tuo account è stato sospeso.",
                    await IndicazioneAssistenzaAsync(utente, cancellationToken));
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
            throw new UnauthorizedAppException(
                "La licenza della tua struttura è scaduta.",
                await IndicazioneAssistenzaAsync(utente, cancellationToken));
        }

        await AzzeraTentativiAsync(utente, cancellationToken);

        // Password giusta ma 2FA attivo: la sessione non nasce qui. Fa eccezione il browser già
        // ricordato, che il codice l'ha comunque dato entro la settimana.
        if (utente.TotpAttivo && !await DispositivoRicordatoAsync(utente.Id, tokenDispositivo, cancellationToken))
        {
            return new EsitoLogin(null, tokenGenerator.GeneraTokenVerifica2Fa(utente).Value);
        }

        return new EsitoLogin(await CreaSessioneAsync(utente, cancellationToken), null);
    }

    /// <summary>
    /// Secondo passo del login: il codice a 6 cifre dell'app, oppure uno dei codici di recupero se
    /// il telefono non c'è più. Un codice sbagliato pesa quanto una password sbagliata — altrimenti
    /// il 2FA diventerebbe il punto debole invece che il contrario.
    /// </summary>
    public async Task<EsitoVerifica2Fa> Verifica2FaAsync(
        string tokenVerifica, string codice, bool ricordaDispositivo, CancellationToken cancellationToken)
    {
        var utenteId = tokenGenerator.LeggiUtenteDaTokenVerifica2Fa(tokenVerifica)
            ?? throw new UnauthorizedAppException("Verifica scaduta: rifai il login.");

        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken);
        if (utente is null || !utente.Attivo || !utente.TotpAttivo || utente.TotpSecret is null)
        {
            throw new UnauthorizedAppException(CredenzialiNonValideMessage);
        }

        if (BloccoAttivo(utente, out var minutiMancanti))
        {
            throw new UnauthorizedAppException(
                $"Troppi tentativi falliti: riprova tra {minutiMancanti} minuti.",
                await IndicazioneAssistenzaAsync(utente, cancellationToken));
        }

        var codiceRecuperoUsato = false;
        if (!totp.VerificaCodice(utente.TotpSecret, codice))
        {
            var codiceRecupero = await TrovaCodiceRecuperoAsync(utente.Id, codice, cancellationToken);
            if (codiceRecupero is null)
            {
                await RegistraTentativoFallitoAsync(utente, cancellationToken);
                await logEventi.RegistraAsync(
                    LivelloLog.Warning,
                    $"Codice di verifica errato per '{utente.Email}'.",
                    origine: "Auth",
                    clienteId: utente.ClienteId,
                    categoria: "Auth",
                    cancellationToken: cancellationToken);
                throw new UnauthorizedAppException("Codice non valido.", await IndicazioneAssistenzaAsync(utente, cancellationToken));
            }

            await dueFattori.SegnaCodiceUsatoAsync(codiceRecupero, cancellationToken);
            codiceRecuperoUsato = true;
        }

        await AzzeraTentativiAsync(utente, cancellationToken);

        string? tokenDispositivo = null;
        DateTime? dispositivoScadeAtUtc = null;
        if (ricordaDispositivo)
        {
            tokenDispositivo = GeneraTokenCasuale();
            dispositivoScadeAtUtc = DateTime.UtcNow.AddDays(GiorniDispositivoFidato);
            await dueFattori.AddDispositivoAsync(
                new DispositivoFidato { UtenteId = utente.Id, TokenHash = Hash(tokenDispositivo), ScadeAtUtc = dispositivoScadeAtUtc.Value },
                cancellationToken);
            await dueFattori.RimuoviDispositiviScadutiAsync(cancellationToken);
        }

        var sessione = await CreaSessioneAsync(utente, cancellationToken);
        var codiciRimasti = (await dueFattori.ListCodiciNonUsatiAsync(utente.Id, cancellationToken)).Count;

        if (codiceRecuperoUsato)
        {
            // Va segnalato forte: o l'utente ha perso il telefono, o sta entrando qualcuno che non
            // dovrebbe avere quei codici.
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                $"Accesso con codice di recupero ({codiciRimasti} ancora disponibili).",
                origine: "Auth",
                clienteId: utente.ClienteId,
                categoria: "Auth",
                operatore: utente.Email,
                cancellationToken: cancellationToken);
        }

        return new EsitoVerifica2Fa(sessione, tokenDispositivo, dispositivoScadeAtUtc, codiciRimasti);
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
    /// <summary>
    /// A chi rivolgersi quando il login non riesce: ognuno sale di un gradino nella propria catena,
    /// mai oltre. Un dipendente si ferma all'amministratore della sua struttura (che può
    /// reimpostargli la password), l'amministratore al titolare dell'account, e solo il titolare —
    /// o il Super Admin, che è GestiSoft — arriva a noi. Prima questa indicazione era una riga fissa
    /// in fondo alla pagina di login che mandava tutti quanti a info@gestisoft.it, compreso chi ha
    /// un amministratore a due passi di distanza che gli risolve il problema in un minuto.
    /// Il ruolo si conosce solo dopo aver trovato l'utente dall'email: per un'email sconosciuta non
    /// viene data alcuna indicazione (vedi UnauthorizedAppException.Assistenza).
    /// </summary>
    private async Task<string> IndicazioneAssistenzaAsync(Utente utente, CancellationToken cancellationToken)
    {
        if (utente.IsSuperAdmin || utente.IsClienteAccount || utente.ClienteId is null)
        {
            return "Se non riesci ad accedere, Contatta GestiSoft: info@gestisoft.it.";
        }

        // "Amministratore" = chi gestisce gli utenti (permesso SettingUser su almeno una Struttura
        // del suo Cliente): è esattamente chi ha in mano il reset della password altrui, quindi chi
        // non ce l'ha deve rivolgersi a lui, e lui a chi sta sopra.
        var amministratore = await utentiStrutture.HaGestioneUtentiClienteAsync(
            utente.Id, utente.ClienteId.Value, cancellationToken);

        return amministratore
            ? "Se non riesci ad accedere, rivolgiti all'amministratore generale del tuo account."
            : "Se non riesci ad accedere, rivolgiti all'amministratore della tua struttura.";
    }

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

    /// <summary>
    /// Avvia l'attivazione: genera il segreto e lo salva, ma NON accende il 2FA — quello succede
    /// solo in <see cref="Attiva2FaAsync"/>, dopo che l'utente ha dimostrato di aver configurato
    /// l'app. Riavviare l'attivazione genera un segreto nuovo: un QR abbandonato a metà non resta
    /// valido per sempre.
    /// </summary>
    public async Task<Avvio2Fa> Avvia2FaAsync(Guid utenteId, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (utente.TotpAttivo)
        {
            throw new ConflictException("La verifica in due passaggi è già attiva: disattivala prima di riconfigurarla.");
        }

        var secret = totp.GeneraSecret();
        utente.TotpSecret = secret;
        await utenti.UpdateAsync(utente, cancellationToken);

        return new Avvio2Fa(secret, totp.CostruisciUriOtpauth(secret, utente.Email));
    }

    /// <summary>
    /// Conferma l'attivazione con il primo codice e restituisce i codici di recupero in chiaro:
    /// è l'unico momento in cui esistono in forma leggibile, dopo restano solo i loro hash.
    /// </summary>
    public async Task<IReadOnlyList<string>> Attiva2FaAsync(Guid utenteId, string codice, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (utente.TotpAttivo)
        {
            throw new ConflictException("La verifica in due passaggi è già attiva.");
        }

        if (utente.TotpSecret is null)
        {
            throw new ConflictException("Configurazione non avviata: rileggi il codice QR.");
        }

        if (!totp.VerificaCodice(utente.TotpSecret, codice))
        {
            throw new UnauthorizedAppException("Codice non valido: controlla di aver inquadrato il QR e che l'ora del telefono sia corretta.");
        }

        utente.TotpAttivo = true;
        utente.TotpAttivatoAtUtc = DateTime.UtcNow;
        await utenti.UpdateAsync(utente, cancellationToken);

        var codiciInChiaro = await RigeneraCodiciRecuperoAsync(utente.Id, cancellationToken);

        await logEventi.RegistraAsync(
            LivelloLog.Info,
            "Verifica in due passaggi attivata.",
            origine: "Auth",
            clienteId: utente.ClienteId,
            categoria: "Auth",
            operatore: utente.Email,
            cancellationToken: cancellationToken);

        return codiciInChiaro;
    }

    /// <summary>
    /// Disattivazione: richiede la password corrente, perché una sessione lasciata aperta su un
    /// computer non deve bastare a togliere la protezione. Porta via anche codici e dispositivi
    /// ricordati — riattivandola si riparte da zero.
    /// </summary>
    public async Task Disattiva2FaAsync(Guid utenteId, string password, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (passwordHasher.VerifyHashedPassword(utente, utente.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAppException("Password errata.");
        }

        utente.TotpAttivo = false;
        utente.TotpSecret = null;
        utente.TotpAttivatoAtUtc = null;
        await utenti.UpdateAsync(utente, cancellationToken);
        await dueFattori.SostituisciCodiciAsync(utente.Id, [], cancellationToken);
        await dueFattori.RimuoviDispositiviAsync(utente.Id, cancellationToken);

        await logEventi.RegistraAsync(
            LivelloLog.Warning,
            "Verifica in due passaggi disattivata.",
            origine: "Auth",
            clienteId: utente.ClienteId,
            categoria: "Auth",
            operatore: utente.Email,
            cancellationToken: cancellationToken);
    }

    /// <summary>Nuovi codici di recupero: invalida i precedenti e i browser ricordati, perché chi li rigenera di solito sospetta di averli persi.</summary>
    public async Task<IReadOnlyList<string>> RigeneraCodiciAsync(Guid utenteId, string password, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (passwordHasher.VerifyHashedPassword(utente, utente.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAppException("Password errata.");
        }

        if (!utente.TotpAttivo)
        {
            throw new ConflictException("La verifica in due passaggi non è attiva.");
        }

        await dueFattori.RimuoviDispositiviAsync(utente.Id, cancellationToken);
        var codici = await RigeneraCodiciRecuperoAsync(utente.Id, cancellationToken);

        await logEventi.RegistraAsync(
            LivelloLog.Warning,
            "Codici di recupero rigenerati: i precedenti non sono più validi.",
            origine: "Auth",
            clienteId: utente.ClienteId,
            categoria: "Auth",
            operatore: utente.Email,
            cancellationToken: cancellationToken);

        return codici;
    }

    public async Task<(bool Attivo, int CodiciRimasti)> Stato2FaAsync(Guid utenteId, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        var rimasti = utente.TotpAttivo ? (await dueFattori.ListCodiciNonUsatiAsync(utenteId, cancellationToken)).Count : 0;
        return (utente.TotpAttivo, rimasti);
    }

    private async Task<LoginResult> CreaSessioneAsync(Utente utente, CancellationToken cancellationToken)
    {
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

    private static bool BloccoAttivo(Utente utente, out int minutiMancanti)
    {
        if (utente.BloccatoFinoUtc is { } bloccatoFino && bloccatoFino > DateTime.UtcNow)
        {
            // Arrotondato per eccesso e mai sotto 1: "riprova tra 0 minuti" non vuol dire niente.
            minutiMancanti = Math.Max(1, (int)Math.Ceiling((bloccatoFino - DateTime.UtcNow).TotalMinutes));
            return true;
        }

        minutiMancanti = 0;
        return false;
    }

    private async Task RegistraTentativoFallitoAsync(Utente utente, CancellationToken cancellationToken)
    {
        utente.TentativiLoginFalliti++;

        if (utente.TentativiLoginFalliti >= MassimiTentativiLogin)
        {
            utente.BloccatoFinoUtc = DateTime.UtcNow.AddMinutes(MinutiBlocco);
            // Il contatore riparte da zero: al termine del blocco l'utente ha di nuovo cinque
            // tentativi, invece di ritrovarsi bloccato al primo errore successivo.
            utente.TentativiLoginFalliti = 0;

            await utenti.UpdateAsync(utente, cancellationToken);
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                $"Account '{utente.Email}' bloccato per {MinutiBlocco} minuti dopo {MassimiTentativiLogin} tentativi falliti.",
                origine: "Auth",
                clienteId: utente.ClienteId,
                categoria: "Auth",
                cancellationToken: cancellationToken);
            return;
        }

        await utenti.UpdateAsync(utente, cancellationToken);
    }

    private async Task AzzeraTentativiAsync(Utente utente, CancellationToken cancellationToken)
    {
        if (utente.TentativiLoginFalliti == 0 && utente.BloccatoFinoUtc is null)
        {
            return;
        }

        utente.TentativiLoginFalliti = 0;
        utente.BloccatoFinoUtc = null;
        await utenti.UpdateAsync(utente, cancellationToken);
    }

    private async Task<bool> DispositivoRicordatoAsync(Guid utenteId, string? tokenDispositivo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenDispositivo))
        {
            return false;
        }

        var dispositivo = await dueFattori.GetDispositivoAsync(Hash(tokenDispositivo), cancellationToken);
        return dispositivo is not null && dispositivo.UtenteId == utenteId && dispositivo.ScadeAtUtc > DateTime.UtcNow;
    }

    private async Task<CodiceRecuperoUtente?> TrovaCodiceRecuperoAsync(Guid utenteId, string codice, CancellationToken cancellationToken)
    {
        var normalizzato = NormalizzaCodiceRecupero(codice);
        if (normalizzato.Length == 0)
        {
            return null;
        }

        var hash = Hash(normalizzato);
        var codici = await dueFattori.ListCodiciNonUsatiAsync(utenteId, cancellationToken);
        return codici.FirstOrDefault(c => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(c.CodiceHash), Encoding.UTF8.GetBytes(hash)));
    }

    private async Task<IReadOnlyList<string>> RigeneraCodiciRecuperoAsync(Guid utenteId, CancellationToken cancellationToken)
    {
        var inChiaro = new List<string>(NumeroCodiciRecupero);
        var righe = new List<CodiceRecuperoUtente>(NumeroCodiciRecupero);

        for (var i = 0; i < NumeroCodiciRecupero; i++)
        {
            var codice = GeneraCodiceRecupero();
            inChiaro.Add(codice);
            righe.Add(new CodiceRecuperoUtente { UtenteId = utenteId, CodiceHash = Hash(NormalizzaCodiceRecupero(codice)) });
        }

        await dueFattori.SostituisciCodiciAsync(utenteId, righe, cancellationToken);
        return inChiaro;
    }

    /// <summary>
    /// 16 caratteri da un alfabeto senza 0/O e 1/I (si trascrivono a mano da un foglio), divisi in
    /// gruppi di 4 per leggibilità: circa 80 bit, quindi indovinabili solo per sbaglio del destino.
    /// </summary>
    private static string GeneraCodiceRecupero()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var caratteri = new char[19];
        var indice = 0;
        for (var gruppo = 0; gruppo < 4; gruppo++)
        {
            if (gruppo > 0)
            {
                caratteri[indice++] = '-';
            }

            for (var i = 0; i < 4; i++)
            {
                caratteri[indice++] = alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
            }
        }

        return new string(caratteri);
    }

    /// <summary>Trattini, spazi e minuscole non contano: l'utente ricopia da un foglio, non deve azzeccare il formato.</summary>
    private static string NormalizzaCodiceRecupero(string codice) =>
        new([.. codice.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)]);

    private static string GeneraTokenCasuale() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// SHA-256 semplice, non un hash da password: qui i valori sono generati a caso con entropia
    /// piena (token da 256 bit, codici da ~80), quindi non c'è nessun dizionario da provare — il
    /// rallentamento di un KDF proteggerebbe da un attacco che non esiste.
    /// </summary>
    private static string Hash(string valore) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valore)));

    private Task LogFallitoAsync(string emailTentata, Guid? clienteId, CancellationToken cancellationToken) =>
        logEventi.RegistraAsync(
            LivelloLog.Warning,
            $"Login fallito per '{emailTentata}'.",
            origine: "Auth",
            clienteId: clienteId,
            categoria: "Auth",
            cancellationToken: cancellationToken);
}
