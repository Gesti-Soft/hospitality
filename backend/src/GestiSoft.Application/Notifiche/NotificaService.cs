using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Ospiti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Notifiche;

/// <summary>
/// Centro notifiche in-app per Struttura (nuova prenotazione, cancellazione, licenza in scadenza,
/// schedine inviate, check-out dimenticato). Le letture/scritture "utente" (Lista/Conta/SegnaLetta)
/// passano da TenantAccessGuard come ogni altra schermata operativa; le scritture "di sistema" (dai
/// job Quartz o dal polling Wubook) non hanno un ICurrentUser e non passano da nessun guard, stesso
/// principio già usato per LogEventoService.
/// </summary>
public class NotificaService(INotificaRepository notifiche, IOspiteRepository ospiti, TenantAccessGuard accessGuard)
{
    private static readonly TimeSpan FinestraGraziaCancellazioneWubook = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<Notifica>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, bool soloNonLette, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);
        return await notifiche.ListaAsync(strutturaId, soloNonLette, cancellationToken);
    }

    public async Task<int> ContaNonLetteAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);
        return await notifiche.ContaNonLetteAsync(strutturaId, cancellationToken);
    }

    public async Task SegnaLettaAsync(ICurrentUser currentUser, Guid strutturaId, Guid notificaId, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);

        var notifica = await notifiche.GetAsync(notificaId, cancellationToken);
        if (notifica is null || notifica.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Notifica non trovata.");
        }

        if (notifica.LettaAtUtc is null)
        {
            notifica.LettaAtUtc = DateTime.UtcNow;
            await notifiche.UpdateAsync(notifica, cancellationToken);
        }
    }

    public async Task SegnaTutteLetteAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);
        await notifiche.SegnaTutteLetteAsync(strutturaId, cancellationToken);
    }

    /// <summary>Notifica idempotente per eventi generati periodicamente da un job (licenza/schedine): non duplica se già creata con la stessa chiave (es. stessa scadenza, stesso giorno).</summary>
    public async Task<bool> CreaSeNonEsisteAsync(Guid strutturaId, TipoNotifica tipo, string chiaveDedup, string titolo, string messaggio, CancellationToken cancellationToken)
    {
        if (await notifiche.EsisteChiaveDedupAsync(strutturaId, chiaveDedup, cancellationToken))
        {
            return false;
        }

        await notifiche.AddAsync(new Notifica
        {
            StrutturaId = strutturaId,
            Tipo = tipo,
            Titolo = titolo,
            Messaggio = messaggio,
            ChiaveDedup = chiaveDedup,
        }, cancellationToken);
        return true;
    }

    /// <summary>Notifica una sola volta per prenotazione (non per giorno): resta finché non viene letta/risolta, non si duplica ad ogni giro del job — usata per "check-out dimenticato".</summary>
    public async Task<bool> CreaPerPrenotazioneSeNonEsisteAsync(Guid strutturaId, TipoNotifica tipo, Guid prenotazioneId, string titolo, string messaggio, CancellationToken cancellationToken)
    {
        if (await notifiche.EsistePerPrenotazioneAsync(strutturaId, tipo, prenotazioneId, cancellationToken))
        {
            return false;
        }

        await notifiche.AddAsync(new Notifica
        {
            StrutturaId = strutturaId,
            Tipo = tipo,
            Titolo = titolo,
            Messaggio = messaggio,
            PrenotazioneId = prenotazioneId,
        }, cancellationToken);
        return true;
    }

    /// <summary>Segna come lette le notifiche di un certo tipo per una prenotazione — usata quando il problema segnalato si risolve da solo (es. check-out effettuato dopo un avviso "dimenticato").</summary>
    public Task RisolviPerPrenotazioneAsync(Guid strutturaId, TipoNotifica tipo, Guid prenotazioneId, CancellationToken cancellationToken) =>
        notifiche.SegnaLettePerPrenotazioneAsync(strutturaId, tipo, prenotazioneId, cancellationToken);

    /// <summary>
    /// Prenotazione Wubook cancellata (status=5): non genera subito una notifica visibile, resta
    /// InAttesa per FinestraGraziaCancellazioneWubook — se entro quella finestra arriva una nuova
    /// prenotazione dallo stesso canale per lo stesso ospite (vedi RegistraNuovaOModificaWubookAsync),
    /// le due vengono fuse in un'unica "Prenotazione modificata" invece di mostrare cancellazione+
    /// nuova separate. Riproduce il comportamento reale di Wubook quando una prenotazione viene
    /// modificata da un canale OTA (cancella il vecchio rcode, ne crea uno nuovo con i dati
    /// aggiornati) — vedi WubookPrenotazioniService.
    /// </summary>
    public Task RegistraCancellazioneWubookAsync(Guid strutturaId, Guid prenotazioneAnnullataId, string canale, string titolo, string messaggio, CancellationToken cancellationToken) =>
        notifiche.AddAsync(new Notifica
        {
            StrutturaId = strutturaId,
            Tipo = TipoNotifica.PrenotazioneAnnullata,
            Stato = StatoNotifica.InAttesa,
            ScadenzaAttesaUtc = DateTime.UtcNow.Add(FinestraGraziaCancellazioneWubook),
            PrenotazioneId = prenotazioneAnnullataId,
            Canale = canale,
            Titolo = titolo,
            Messaggio = messaggio,
        }, cancellationToken);

    /// <summary>
    /// Prenotazione Wubook nuova o aggiornata. Se esiste una cancellazione pendente (non scaduta)
    /// sullo stesso canale per lo stesso ospite (stessa email, o stesso nome+cognome se l'email manca
    /// su uno dei due lati), la promuove a "Prenotazione modificata" riferita alla nuova prenotazione
    /// invece di generare una "Nuova prenotazione" separata. Il match è solo su canale+ospite (mai su
    /// camera/date), perché quelli sono proprio i campi che una modifica reale può cambiare.
    /// </summary>
    public async Task RegistraNuovaOModificaWubookAsync(
        Guid strutturaId,
        Guid nuovaPrenotazioneId,
        string canale,
        string? email,
        string? nome,
        string? cognome,
        string titoloNuova,
        string messaggioNuova,
        string titoloModifica,
        string messaggioModifica,
        CancellationToken cancellationToken)
    {
        var pendenti = await notifiche.ListCancellazioniPendentiAsync(strutturaId, canale, cancellationToken);
        foreach (var pendente in pendenti)
        {
            if (pendente.PrenotazioneId is not { } prenotazioneCancellataId)
            {
                continue;
            }

            var ospiteCancellato = await ospiti.GetByPrenotazioneAsync(prenotazioneCancellataId, cancellationToken);
            if (ospiteCancellato is null)
            {
                continue;
            }

            var stessoOspite = !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(ospiteCancellato.Email)
                ? string.Equals(email, ospiteCancellato.Email, StringComparison.OrdinalIgnoreCase)
                : string.Equals(nome, ospiteCancellato.Nome, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(cognome, ospiteCancellato.Cognome, StringComparison.OrdinalIgnoreCase);

            if (!stessoOspite)
            {
                continue;
            }

            pendente.Tipo = TipoNotifica.PrenotazioneModificata;
            pendente.Stato = StatoNotifica.Confermata;
            pendente.ScadenzaAttesaUtc = null;
            pendente.PrenotazioneId = nuovaPrenotazioneId;
            pendente.CreatedAtUtc = DateTime.UtcNow;
            pendente.Titolo = titoloModifica;
            pendente.Messaggio = messaggioModifica;
            await notifiche.UpdateAsync(pendente, cancellationToken);
            return;
        }

        await notifiche.AddAsync(new Notifica
        {
            StrutturaId = strutturaId,
            Tipo = TipoNotifica.NuovaPrenotazione,
            PrenotazioneId = nuovaPrenotazioneId,
            Canale = canale,
            Titolo = titoloNuova,
            Messaggio = messaggioNuova,
        }, cancellationToken);
    }

    /// <summary>
    /// Rete di sicurezza per un arrivo mai segnalato: crea la notifica di nuova prenotazione solo se
    /// per quella prenotazione non ne esiste già una di arrivo (nuova o modificata). Serve quando un
    /// import si interrompe dopo aver salvato la prenotazione ma prima della notifica — i passi non
    /// sono in transazione — e viene poi ritentato: al secondo giro la prenotazione non risulta più
    /// "nuova", quindi senza questo recupero il suo arrivo non verrebbe segnalato mai più.
    /// Ritorna true se la notifica è stata effettivamente creata adesso.
    /// </summary>
    public async Task<bool> RecuperaArrivoNonNotificatoAsync(Guid strutturaId, Guid prenotazioneId, string canale, string titolo, string messaggio, CancellationToken cancellationToken)
    {
        if (await notifiche.EsisteArrivoPerPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken))
        {
            return false;
        }

        await notifiche.AddAsync(new Notifica
        {
            StrutturaId = strutturaId,
            Tipo = TipoNotifica.NuovaPrenotazione,
            PrenotazioneId = prenotazioneId,
            Canale = canale,
            Titolo = titolo,
            Messaggio = messaggio,
        }, cancellationToken);
        return true;
    }

    /// <summary>
    /// Prenotazione Wubook già nota (stesso rcode) i cui dati sono cambiati — caso diverso da
    /// RegistraNuovaOModificaWubookAsync, che copre la modifica fatta da Wubook come cancella+
    /// ricrea: qui l'rcode resta lo stesso e non c'è nessuna cancellazione da fondere, quindi
    /// nessuna finestra di grazia e nessun match sull'ospite. Non deduplicata: ogni modifica reale
    /// è un evento a sé (chi ha già letto la precedente deve vedere anche la successiva); a filtrare
    /// le ri-elaborazioni identiche ci pensa il chiamante, che notifica solo a differenze presenti.
    /// </summary>
    public Task RegistraModificaWubookAsync(Guid strutturaId, Guid prenotazioneId, string canale, string titolo, string messaggio, CancellationToken cancellationToken) =>
        notifiche.AddAsync(new Notifica
        {
            StrutturaId = strutturaId,
            Tipo = TipoNotifica.PrenotazioneModificata,
            PrenotazioneId = prenotazioneId,
            Canale = canale,
            Titolo = titolo,
            Messaggio = messaggio,
        }, cancellationToken);

    /// <summary>
    /// Da chiamare ad ogni giro del polling eventi Wubook (una volta al minuto per Struttura, vedi
    /// WubookEventiService): promuove a "cancellata" visibile le cancellazioni la cui finestra di
    /// grazia è scaduta senza che sia arrivata una prenotazione corrispondente.
    /// </summary>
    public async Task ConfermaCancellazioniScaduteAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var scadute = await notifiche.ListCancellazioniPendentiScaduteAsync(strutturaId, DateTime.UtcNow, cancellationToken);
        foreach (var notifica in scadute)
        {
            notifica.Stato = StatoNotifica.Confermata;
            await notifiche.UpdateAsync(notifica, cancellationToken);
        }
    }
}
