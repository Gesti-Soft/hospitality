using GestiSoft.Application.Auth;
using GestiSoft.Application.Common;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Assistenza;

/// <summary>Foto ricevuta dal browser, non ancora controllata né salvata.</summary>
public record NuovoAllegato(string NomeFile, byte[] Contenuto);

/// <summary>
/// Ticket di assistenza tra le Strutture e lo staff GestiSoft. Lato struttura li apre e li legge
/// solo chi gestisce gli utenti (titolare o permesso SettingUser), non un operatore qualsiasi; lato
/// staff solo il Super Admin. Le email vanno al Super Admin quando la struttura scrive e al solo
/// titolare quando risponde lo staff — mai agli operatori, nemmeno a chi ha aperto il ticket.
/// </summary>
public class AssistenzaService(
    ITicketRepository tickets,
    IAllegatiStorage allegatiStorage,
    IEmailSender email,
    IStrutturaRepository strutture,
    IUtenteRepository utenti,
    TenantAccessGuard accessGuard,
    GestioneUtentiGuard gestioneUtentiGuard,
    ILogEventoService logEventi)
{
    public const int OggettoMaxCaratteri = 150;
    public const int TestoMaxCaratteri = 5000;
    public const int AllegatiMaxPerMessaggio = 5;
    public const int AllegatoMaxByte = 5 * 1024 * 1024;

    /// <summary>Mesi dopo la chiusura oltre i quali un ticket viene anonimizzato, vedi <see cref="AnonimizzaScadutiAsync"/>.</summary>
    public const int MesiConservazione = 12;

    private const string CategoriaLog = "Assistenza";
    private const string NomeStaff = "Assistenza GestiSoft";

    // ---- Lato struttura -------------------------------------------------------------------

    public async Task<IReadOnlyList<TicketRiepilogo>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await EnsureStrutturaAsync(currentUser, strutturaId, cancellationToken);
        return await tickets.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<int> ContaNonLettiAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await EnsureStrutturaAsync(currentUser, strutturaId, cancellationToken);
        return await tickets.ContaNonLettiStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<TicketRiepilogo> DettaglioAsync(ICurrentUser currentUser, Guid strutturaId, Guid ticketId, CancellationToken cancellationToken)
    {
        await EnsureStrutturaAsync(currentUser, strutturaId, cancellationToken);
        return await GetDellaStrutturaAsync(strutturaId, ticketId, cancellationToken);
    }

    public async Task SegnaLettoAsync(ICurrentUser currentUser, Guid strutturaId, Guid ticketId, CancellationToken cancellationToken)
    {
        await EnsureStrutturaAsync(currentUser, strutturaId, cancellationToken);
        var dettaglio = await GetDellaStrutturaAsync(strutturaId, ticketId, cancellationToken);

        dettaglio.Ticket.LettoClienteAtUtc = DateTime.UtcNow;
        await tickets.SalvaAsync(cancellationToken);
    }

    public async Task<TicketRiepilogo> ApriAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        string oggetto,
        string testo,
        IReadOnlyList<NuovoAllegato> allegati,
        CancellationToken cancellationToken)
    {
        await EnsureStrutturaAsync(currentUser, strutturaId, cancellationToken);

        oggetto = oggetto.Trim();
        if (oggetto.Length == 0)
        {
            throw new ConflictException("Scrivi l'oggetto del ticket: una riga che riassume il problema.");
        }

        if (oggetto.Length > OggettoMaxCaratteri)
        {
            throw new ConflictException($"L'oggetto può essere lungo al massimo {OggettoMaxCaratteri} caratteri: i dettagli vanno nel messaggio.");
        }

        testo = ValidaTesto(testo);
        var controllati = ControllaAllegati(allegati);

        var adesso = DateTime.UtcNow;
        var ticket = new Ticket
        {
            StrutturaId = strutturaId,
            Oggetto = oggetto,
            AutoreUtenteId = currentUser.UtenteId,
            UltimoMessaggioClienteAtUtc = adesso,
            // Chi l'ha aperto l'ha ovviamente già letto: senza, il ticket appena creato comparirebbe
            // subito tra i non letti della struttura.
            LettoClienteAtUtc = adesso,
        };

        var messaggio = new TicketMessaggio
        {
            StrutturaId = strutturaId,
            Ticket = ticket,
            DaStaff = false,
            AutoreUtenteId = currentUser.UtenteId,
            Testo = testo,
        };
        ticket.Messaggi.Add(messaggio);

        var salvati = await SalvaAllegatiAsync(strutturaId, messaggio, controllati, cancellationToken);
        try
        {
            await tickets.AddAsync(ticket, cancellationToken);
        }
        catch
        {
            EliminaFileSalvati(salvati);
            throw;
        }

        var dettaglio = await tickets.GetDettaglioAsync(ticket.Id, cancellationToken)
            ?? throw new NotFoundException("Ticket non trovato.");

        await logEventi.RegistraAsync(
            LivelloLog.Info,
            $"Aperto il ticket di assistenza n. {ticket.Numero}.",
            origine: "Api",
            clienteId: currentUser.ClienteId,
            strutturaId: strutturaId,
            categoria: CategoriaLog,
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

        await email.InviaAdAssistenzaAsync(
            $"[Assistenza] Nuovo ticket n. {ticket.Numero}: {ticket.Oggetto}",
            $"{dettaglio.ClienteRagioneSociale} · {dettaglio.StrutturaNome}\n"
            + $"Aperto da {await NomeAutoreAsync(currentUser.UtenteId, cancellationToken)}\n\n"
            + testo
            + RigaFoto(controllati.Count),
            $"/super-admin/assistenza?ticket={ticket.Id}",
            cancellationToken);

        return dettaglio;
    }

    public async Task<TicketRiepilogo> RispondiAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        Guid ticketId,
        string testo,
        IReadOnlyList<NuovoAllegato> allegati,
        CancellationToken cancellationToken)
    {
        await EnsureStrutturaAsync(currentUser, strutturaId, cancellationToken);
        var dettaglio = await GetDellaStrutturaAsync(strutturaId, ticketId, cancellationToken);
        var ticket = dettaglio.Ticket;
        EnsureAperto(ticket);

        testo = ValidaTesto(testo);
        var controllati = ControllaAllegati(allegati);

        var adesso = DateTime.UtcNow;
        var messaggio = new TicketMessaggio
        {
            StrutturaId = strutturaId,
            TicketId = ticket.Id,
            DaStaff = false,
            AutoreUtenteId = currentUser.UtenteId,
            Testo = testo,
        };

        ticket.UltimoMessaggioClienteAtUtc = adesso;
        ticket.LettoClienteAtUtc = adesso;
        ticket.UpdatedAtUtc = adesso;

        await AggiungiMessaggioAsync(strutturaId, messaggio, controllati, cancellationToken);

        await email.InviaAdAssistenzaAsync(
            $"[Assistenza] Nuovo messaggio sul ticket n. {ticket.Numero}: {ticket.Oggetto}",
            $"{dettaglio.ClienteRagioneSociale} · {dettaglio.StrutturaNome}\n"
            + $"Scrive {await NomeAutoreAsync(currentUser.UtenteId, cancellationToken)}\n\n"
            + testo
            + RigaFoto(controllati.Count),
            $"/super-admin/assistenza?ticket={ticket.Id}",
            cancellationToken);

        return await tickets.GetDettaglioAsync(ticket.Id, cancellationToken) ?? throw new NotFoundException("Ticket non trovato.");
    }

    public async Task<(Stream Contenuto, string ContentType)> ApriAllegatoAsync(ICurrentUser currentUser, Guid strutturaId, Guid allegatoId, CancellationToken cancellationToken)
    {
        await EnsureStrutturaAsync(currentUser, strutturaId, cancellationToken);

        var allegato = await tickets.GetAllegatoAsync(allegatoId, cancellationToken);
        if (allegato is null || allegato.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Foto non trovata.");
        }

        return ApriFile(allegato);
    }

    // ---- Lato staff (Super Admin) ---------------------------------------------------------

    public async Task<IReadOnlyList<TicketRiepilogo>> ListaTuttiAsync(ICurrentUser currentUser, bool soloAperti, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await tickets.ListTuttiAsync(soloAperti, cancellationToken);
    }

    public async Task<int> ContaNonLettiStaffAsync(ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await tickets.ContaNonLettiStaffAsync(cancellationToken);
    }

    public async Task<TicketRiepilogo> DettaglioStaffAsync(ICurrentUser currentUser, Guid ticketId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await tickets.GetDettaglioAsync(ticketId, cancellationToken) ?? throw new NotFoundException("Ticket non trovato.");
    }

    public async Task SegnaLettoStaffAsync(ICurrentUser currentUser, Guid ticketId, CancellationToken cancellationToken)
    {
        var dettaglio = await DettaglioStaffAsync(currentUser, ticketId, cancellationToken);

        dettaglio.Ticket.LettoStaffAtUtc = DateTime.UtcNow;
        await tickets.SalvaAsync(cancellationToken);
    }

    public async Task<TicketRiepilogo> RispondiStaffAsync(
        ICurrentUser currentUser,
        Guid ticketId,
        string testo,
        IReadOnlyList<NuovoAllegato> allegati,
        CancellationToken cancellationToken)
    {
        var dettaglio = await DettaglioStaffAsync(currentUser, ticketId, cancellationToken);
        var ticket = dettaglio.Ticket;
        EnsureAperto(ticket);

        testo = ValidaTesto(testo);
        var controllati = ControllaAllegati(allegati);

        var adesso = DateTime.UtcNow;
        var messaggio = new TicketMessaggio
        {
            StrutturaId = ticket.StrutturaId,
            TicketId = ticket.Id,
            DaStaff = true,
            AutoreUtenteId = currentUser.UtenteId,
            Testo = testo,
        };

        ticket.UltimoMessaggioStaffAtUtc = adesso;
        ticket.LettoStaffAtUtc = adesso;
        ticket.UpdatedAtUtc = adesso;

        await AggiungiMessaggioAsync(ticket.StrutturaId, messaggio, controllati, cancellationToken);

        // Solo al titolare, su richiesta esplicita: gli operatori vedono la risposta nella pagina
        // Assistenza, ma la casella a cui scrive GestiSoft è quella del Cliente.
        var titolare = await TitolareAsync(ticket.StrutturaId, cancellationToken);
        if (titolare is not null)
        {
            await email.InviaAsync(
                titolare.Email,
                $"Risposta dell'assistenza GestiSoft · ticket n. {ticket.Numero}",
                $"C'è una nuova risposta al ticket n. {ticket.Numero} «{ticket.Oggetto}» per la struttura {dettaglio.StrutturaNome}.\n\n"
                + testo
                + RigaFoto(controllati.Count),
                $"/assistenza?ticket={ticket.Id}",
                cancellationToken);
        }

        return await tickets.GetDettaglioAsync(ticket.Id, cancellationToken) ?? throw new NotFoundException("Ticket non trovato.");
    }

    /// <summary>
    /// Chiude il ticket e cancella dal disco tutte le sue foto. Il testo dei messaggi resta. Un file
    /// che non si riesce a cancellare resta segnato come presente (e quindi visibile), così non se
    /// ne perde traccia: finirebbe altrimenti per occupare spazio senza che nessuno lo sappia.
    /// </summary>
    public async Task<TicketRiepilogo> ChiudiAsync(ICurrentUser currentUser, Guid ticketId, CancellationToken cancellationToken)
    {
        var dettaglio = await DettaglioStaffAsync(currentUser, ticketId, cancellationToken);
        var ticket = dettaglio.Ticket;
        if (ticket.Stato == StatoTicket.Chiuso)
        {
            return dettaglio;
        }

        var adesso = DateTime.UtcNow;
        var eliminati = 0;
        var nonEliminati = 0;
        foreach (var allegato in ticket.Messaggi.SelectMany(m => m.Allegati).Where(a => a.EliminatoAtUtc is null))
        {
            if (allegatiStorage.Elimina(allegato.Percorso))
            {
                allegato.EliminatoAtUtc = adesso;
                eliminati++;
            }
            else
            {
                nonEliminati++;
            }
        }

        ticket.Stato = StatoTicket.Chiuso;
        ticket.ChiusoAtUtc = adesso;
        ticket.LettoStaffAtUtc = adesso;
        ticket.UpdatedAtUtc = adesso;
        await tickets.SalvaAsync(cancellationToken);

        await logEventi.RegistraAsync(
            nonEliminati == 0 ? LivelloLog.Info : LivelloLog.Warning,
            nonEliminati == 0
                ? $"Chiuso il ticket di assistenza n. {ticket.Numero}, foto cancellate: {eliminati}."
                : $"Chiuso il ticket di assistenza n. {ticket.Numero}: {nonEliminati} foto non si sono potute cancellare dal disco.",
            origine: "Api",
            strutturaId: ticket.StrutturaId,
            categoria: CategoriaLog,
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

        return dettaglio;
    }

    public async Task<(Stream Contenuto, string ContentType)> ApriAllegatoStaffAsync(ICurrentUser currentUser, Guid allegatoId, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);

        var allegato = await tickets.GetAllegatoAsync(allegatoId, cancellationToken)
            ?? throw new NotFoundException("Foto non trovata.");

        return ApriFile(allegato);
    }

    // ---- Conservazione (job del Worker, nessun utente) ------------------------------------

    /// <summary>
    /// Conservazione GDPR (art. 5.1.e), scelta dell'utente: 12 mesi dopo la chiusura di un ticket si
    /// cancellano messaggi e allegati e si scollega l'autore. Restano numero, oggetto, struttura,
    /// stato e date, per sapere quanti ticket ci sono stati e su cosa. Un ticket con una foto ancora
    /// sul disco (cancellazione alla chiusura non riuscita) si salta finché il file non va via: se se
    /// ne perdesse la riga, quel file non lo troverebbe più nessuno.
    /// </summary>
    public async Task<int> AnonimizzaScadutiAsync(CancellationToken cancellationToken)
    {
        var adesso = DateTime.UtcNow;
        var daAnonimizzare = await tickets.ListDaAnonimizzareAsync(adesso.AddMonths(-MesiConservazione), cancellationToken);
        if (daAnonimizzare.Count == 0)
        {
            return 0;
        }

        var bloccati = new HashSet<Guid>();
        foreach (var allegato in await tickets.ListAllegatiRimastiAsync(daAnonimizzare, cancellationToken))
        {
            if (!allegatiStorage.Elimina(allegato.Percorso))
            {
                bloccati.Add(allegato.MessaggioId);
            }
        }

        var pronti = bloccati.Count == 0
            ? daAnonimizzare
            : await TicketSenzaMessaggiBloccatiAsync(daAnonimizzare, bloccati, cancellationToken);

        if (pronti.Count > 0)
        {
            await tickets.AnonimizzaAsync(pronti, adesso, cancellationToken);
        }

        if (pronti.Count < daAnonimizzare.Count)
        {
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                $"Assistenza: {daAnonimizzare.Count - pronti.Count} ticket non anonimizzati perché una foto non si è potuta cancellare dal disco. Si riprova al prossimo giro.",
                origine: "Worker",
                categoria: CategoriaLog,
                cancellationToken: cancellationToken);
        }

        return pronti.Count;
    }

    private async Task<IReadOnlyList<Guid>> TicketSenzaMessaggiBloccatiAsync(IReadOnlyList<Guid> ticketIds, HashSet<Guid> messaggiBloccati, CancellationToken cancellationToken)
    {
        var pronti = new List<Guid>(ticketIds.Count);
        foreach (var ticketId in ticketIds)
        {
            var dettaglio = await tickets.GetDettaglioAsync(ticketId, cancellationToken);
            if (dettaglio is not null && !dettaglio.Ticket.Messaggi.Any(m => messaggiBloccati.Contains(m.Id)))
            {
                pronti.Add(ticketId);
            }
        }

        return pronti;
    }

    // ---- Condivisi ------------------------------------------------------------------------

    /// <summary>Per la struttura: c'è una risposta dello staff più recente dell'ultima lettura.</summary>
    public static bool NonLettoDallaStruttura(Ticket t) =>
        t.UltimoMessaggioStaffAtUtc is { } staff && (t.LettoClienteAtUtc is not { } letto || letto < staff);

    /// <summary>Per lo staff: c'è un messaggio della struttura più recente dell'ultima lettura.</summary>
    public static bool NonLettoDalloStaff(Ticket t) =>
        t.Stato == StatoTicket.Aperto && (t.LettoStaffAtUtc is not { } letto || letto < t.UltimoMessaggioClienteAtUtc);

    public static string NomeAutore(TicketMessaggio m)
    {
        if (m.DaStaff)
        {
            return NomeStaff;
        }

        if (m.AutoreUtente is not { } utente)
        {
            return "Utente eliminato";
        }

        var nome = $"{utente.Nome} {utente.Cognome}".Trim();
        return nome.Length > 0 ? nome : utente.Email;
    }

    /// <summary>
    /// Il Super Admin entrato in una struttura vede il menu di quel Cliente, compresa la pagina
    /// Assistenza: da lì però non deve poter aprire ticket a se stesso. Per lui c'è il pannello.
    /// </summary>
    private async Task EnsureStrutturaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Il Super Admin gestisce i ticket dalla pagina Assistenza del pannello amministratore.");
        }

        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);
        await gestioneUtentiGuard.EnsureAsync(currentUser, strutturaId, cancellationToken);
    }

    private async Task<TicketRiepilogo> GetDellaStrutturaAsync(Guid strutturaId, Guid ticketId, CancellationToken cancellationToken)
    {
        var dettaglio = await tickets.GetDettaglioAsync(ticketId, cancellationToken);
        if (dettaglio is null || dettaglio.Ticket.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Ticket non trovato.");
        }

        return dettaglio;
    }

    private static void RichiediSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può gestire i ticket di assistenza.");
        }
    }

    private static void EnsureAperto(Ticket ticket)
    {
        if (ticket.Stato == StatoTicket.Chiuso)
        {
            throw new ConflictException("Questo ticket è chiuso. Per lo stesso problema apri un ticket nuovo.");
        }
    }

    private static string ValidaTesto(string testo)
    {
        testo = testo.Trim();
        if (testo.Length == 0)
        {
            throw new ConflictException("Scrivi il messaggio prima di inviarlo.");
        }

        if (testo.Length > TestoMaxCaratteri)
        {
            throw new ConflictException($"Il messaggio può essere lungo al massimo {TestoMaxCaratteri} caratteri.");
        }

        return testo;
    }

    private sealed record AllegatoControllato(string NomeFile, byte[] Contenuto, string ContentType, string Estensione);

    private static List<AllegatoControllato> ControllaAllegati(IReadOnlyList<NuovoAllegato> allegati)
    {
        if (allegati.Count > AllegatiMaxPerMessaggio)
        {
            throw new ConflictException($"Puoi allegare al massimo {AllegatiMaxPerMessaggio} foto per messaggio.");
        }

        var controllati = new List<AllegatoControllato>(allegati.Count);
        foreach (var allegato in allegati)
        {
            var nome = NomeFileSicuro(allegato.NomeFile);

            if (allegato.Contenuto.Length == 0)
            {
                throw new ConflictException($"La foto {nome} è vuota.");
            }

            if (allegato.Contenuto.Length > AllegatoMaxByte)
            {
                throw new ConflictException($"La foto {nome} supera il limite di {AllegatoMaxByte / (1024 * 1024)} MB.");
            }

            // Il formato si riconosce dai byte, non dal content-type dichiarato dal browser: lo
            // sceglie chi carica, e il file verrà poi servito con il tipo che dichiariamo noi.
            var (contentType, estensione) = RiconosciFormato(allegato.Contenuto)
                ?? throw new ConflictException($"{nome} non è una foto: si possono allegare solo immagini PNG, JPEG o WebP.");

            controllati.Add(new AllegatoControllato(nome, allegato.Contenuto, contentType, estensione));
        }

        return controllati;
    }

    private async Task AggiungiMessaggioAsync(Guid strutturaId, TicketMessaggio messaggio, List<AllegatoControllato> controllati, CancellationToken cancellationToken)
    {
        var salvati = await SalvaAllegatiAsync(strutturaId, messaggio, controllati, cancellationToken);
        try
        {
            await tickets.AddMessaggioAsync(messaggio, cancellationToken);
        }
        catch
        {
            EliminaFileSalvati(salvati);
            throw;
        }
    }

    /// <summary>I file si scrivono prima della riga nel database: se poi il salvataggio fallisce, il chiamante li cancella con <see cref="EliminaFileSalvati"/>.</summary>
    private async Task<List<string>> SalvaAllegatiAsync(Guid strutturaId, TicketMessaggio messaggio, List<AllegatoControllato> controllati, CancellationToken cancellationToken)
    {
        var salvati = new List<string>(controllati.Count);
        try
        {
            foreach (var a in controllati)
            {
                var percorso = await allegatiStorage.SalvaAsync(strutturaId, a.Contenuto, a.Estensione, cancellationToken);
                salvati.Add(percorso);

                messaggio.Allegati.Add(new TicketAllegato
                {
                    StrutturaId = strutturaId,
                    Messaggio = messaggio,
                    NomeFile = a.NomeFile,
                    ContentType = a.ContentType,
                    DimensioneByte = a.Contenuto.Length,
                    Percorso = percorso,
                });
            }
        }
        catch
        {
            EliminaFileSalvati(salvati);
            throw;
        }

        return salvati;
    }

    private void EliminaFileSalvati(List<string> percorsi)
    {
        foreach (var percorso in percorsi)
        {
            allegatiStorage.Elimina(percorso);
        }
    }

    private (Stream Contenuto, string ContentType) ApriFile(TicketAllegato allegato)
    {
        if (allegato.EliminatoAtUtc is not null)
        {
            throw new NotFoundException("Questa foto è stata cancellata alla chiusura del ticket.");
        }

        var contenuto = allegatiStorage.Apri(allegato.Percorso)
            ?? throw new NotFoundException("Il file di questa foto non si trova più sul server.");

        return (contenuto, allegato.ContentType);
    }

    private async Task<Utente?> TitolareAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken);
        if (struttura is null)
        {
            return null;
        }

        var utentiCliente = await utenti.ListByClienteIdAsync(struttura.ClienteId, cancellationToken);
        return utentiCliente.FirstOrDefault(u => u.IsClienteAccount && u.Attivo && !string.IsNullOrWhiteSpace(u.Email));
    }

    private async Task<string> NomeAutoreAsync(Guid utenteId, CancellationToken cancellationToken)
    {
        var utente = await utenti.GetByIdAsync(utenteId, cancellationToken);
        if (utente is null)
        {
            return "utente sconosciuto";
        }

        var nome = $"{utente.Nome} {utente.Cognome}".Trim();
        return nome.Length > 0 ? $"{nome} ({utente.Email})" : utente.Email;
    }

    private static string RigaFoto(int numero) => numero switch
    {
        0 => string.Empty,
        1 => "\n\n(1 foto allegata, visibile nel gestionale)",
        _ => $"\n\n({numero} foto allegate, visibili nel gestionale)",
    };

    private static string NomeFileSicuro(string nomeFile)
    {
        var nome = Path.GetFileName(nomeFile ?? string.Empty).Trim();
        if (nome.Length == 0)
        {
            return "foto";
        }

        return nome.Length > 120 ? nome[..120] : nome;
    }

    private static (string ContentType, string Estensione)? RiconosciFormato(ReadOnlySpan<byte> contenuto)
    {
        ReadOnlySpan<byte> firmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (contenuto.Length >= firmaPng.Length && contenuto[..firmaPng.Length].SequenceEqual(firmaPng))
        {
            return ("image/png", ".png");
        }

        if (contenuto.Length >= 3 && contenuto[0] == 0xFF && contenuto[1] == 0xD8 && contenuto[2] == 0xFF)
        {
            return ("image/jpeg", ".jpg");
        }

        // WebP: "RIFF" + 4 byte di lunghezza + "WEBP".
        if (contenuto.Length >= 12
            && contenuto[..4].SequenceEqual("RIFF"u8)
            && contenuto.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ("image/webp", ".webp");
        }

        return null;
    }
}
