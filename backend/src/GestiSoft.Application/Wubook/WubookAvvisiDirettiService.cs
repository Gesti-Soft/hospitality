using System.Collections.Concurrent;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Wubook;

/// <summary>Stato della ricezione diretta per il pannello Super Admin. <see cref="UrlRegistrato"/> null = l'OTA non l'ha detto (errore o nessun indirizzo).</summary>
public record StatoAvvisiDiretti(bool Attivi, bool IndirizzoCorretto, string? UrlRegistrato, string? UrlPrecedente, string? ErroreLettura);

/// <summary>
/// Prenotazioni che arrivano direttamente dall'OTA, senza passare da gestisoft.it
/// (tdocs.wubook.net/wired/fetch.html, "push notifications"). L'OTA manda una POST con lcode e rcode
/// a ogni prenotazione nuova, modificata o cancellata; l'Api la registra e risponde subito
/// (<see cref="RegistraAvvisoAsync"/>), il Worker la scarica con fetch_booking e la importa
/// (<see cref="ElaboraAsync"/>). Le importazioni le fa solo il Worker: con due processi la stessa
/// prenotazione nuova potrebbe essere creata due volte.
///
/// Rete di sicurezza: ogni <see cref="IntervalloControllo"/> si scaricano le prenotazioni create negli
/// ultimi giorni (fetch_bookings, non segna nulla come letto) e si importano quelle che qui mancano
/// o che l'OTA dà cancellate, così un avviso perso non fa perdere una prenotazione.
///
/// Con la ricezione diretta attiva non si usano fetch_new_bookings né gestisoft.it per quella
/// struttura: la documentazione chiede di non mischiare i due modi. Disattivando, l'OTA torna a
/// mandare gli avvisi all'indirizzo di prima (gestisoft.it).
/// </summary>
public class WubookAvvisiDirettiService(
    IWubookIntegrazioneRepository integrazioni,
    IWubookEventoRicevutoRepository eventiRicevuti,
    IWubookClient wubookClient,
    WubookLicenzaService licenzaService,
    WubookPrenotazioniService prenotazioniService,
    IStrutturaRepository strutture,
    ILogEventoService logEventi)
{
    /// <summary>
    /// Tentativi di importazione di un avviso prima di smettere. Le attese crescono (1, 2, 4… minuti,
    /// poi un'ora fissa, vedi <see cref="AttesaDopo"/>): 30 tentativi coprono circa 24 ore, così
    /// un'interruzione dell'OTA anche di qualche ora non fa perdere la prenotazione.
    /// </summary>
    public const int TentativiMassimi = 30;

    /// <summary>Dopo quanti tentativi falliti si avvisa nel Log, mentre si continua a riprovare.</summary>
    public const int TentativiPrimaDellAvviso = 5;

    /// <summary>Attesa prima del tentativo successivo al numero <paramref name="tentativiFalliti"/>: 1, 2, 4, 8, 16, 32 minuti, poi 60.</summary>
    public static TimeSpan AttesaDopo(int tentativiFalliti) =>
        TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, tentativiFalliti - 1))));

    /// <summary>
    /// Ogni quanto si controllano le prenotazioni recenti. fetch_bookings è limitato a 288 chiamate
    /// ogni 12 ore per struttura: ogni 15 minuti sono 48.
    /// </summary>
    public static readonly TimeSpan IntervalloControllo = TimeSpan.FromMinutes(15);

    /// <summary>Quanti giorni indietro (per data di creazione) guarda il controllo periodico.</summary>
    public const int GiorniControllo = 3;

    // Ultimo controllo per struttura: in memoria, dopo un riavvio del Worker il primo giro controlla subito.
    private static readonly ConcurrentDictionary<Guid, DateTime> UltimoControllo = new();

    /// <summary>
    /// Avviso ricevuto dall'Api: si registra soltanto (risposta immediata all'OTA, che altrimenti
    /// riprova e con troppi errori sospende gli avvisi). Un avviso per una struttura sconosciuta o
    /// senza rcode valido (la prova dell'attivazione) va solo nel Log. <paramref name="dettaglio"/>:
    /// com'era fatta la richiesta, quando non se ne sono letti i codici.
    /// </summary>
    public async Task RegistraAvvisoAsync(string? lcode, string? rcodeTesto, string? dettaglio, CancellationToken cancellationToken)
    {
        var codice = lcode?.Trim();
        if (string.IsNullOrEmpty(codice))
        {
            // Il controllo periodico recupera comunque le prenotazioni nuove: qui si annota cosa è arrivato.
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                $"Avviso dell'OTA ricevuto senza codice struttura (rcode={rcodeTesto ?? "assente"}): non si sa a quale struttura appartiene. Se è appena stata attivata la ricezione diretta è la prova dell'attivazione.",
                origine: "Wubook",
                dettaglio: dettaglio,
                categoria: "Wubook",
                cancellationToken: cancellationToken);
            return;
        }

        var integrazione = (await integrazioni.ListAttiveAsync(cancellationToken))
            .FirstOrDefault(i => i.AvvisiDiretti && string.Equals(i.CodiceStruttura?.Trim(), codice, StringComparison.Ordinal));
        if (integrazione is null)
        {
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                $"Avviso dell'OTA per un codice struttura senza ricezione diretta attiva (lcode={codice}, rcode={rcodeTesto ?? "assente"}): ignorato.",
                origine: "Wubook",
                dettaglio: dettaglio,
                categoria: "Wubook",
                cancellationToken: cancellationToken);
            return;
        }

        var clienteId = await strutture.GetClienteIdAsync(integrazione.StrutturaId, cancellationToken);
        if (!int.TryParse(rcodeTesto?.Trim(), out var rcode) || rcode <= 0)
        {
            await logEventi.RegistraAsync(
                LivelloLog.Info,
                $"Avviso dell'OTA ricevuto senza un codice prenotazione valido (rcode={rcodeTesto ?? "assente"}): è la prova dell'attivazione, la ricezione diretta funziona.",
                origine: "Wubook",
                dettaglio: dettaglio,
                clienteId: clienteId,
                strutturaId: integrazione.StrutturaId,
                categoria: "Wubook",
                cancellationToken: cancellationToken);
            return;
        }

        await eventiRicevuti.SegnaDaElaborareAsync(integrazione.StrutturaId, rcode, codice!, DateTime.UtcNow, cancellationToken);
    }

    /// <summary>
    /// Giro del Worker (ogni minuto) per una struttura: avvisi da elaborare e, se è ora, il controllo
    /// periodico. Gli avvisi rimasti si elaborano anche se nel frattempo la ricezione diretta è stata
    /// disattivata: sono prenotazioni già annunciate dall'OTA.
    /// </summary>
    public async Task ElaboraAsync(WubookIntegrazione integrazione, CancellationToken cancellationToken)
    {
        var strutturaId = integrazione.StrutturaId;
        var daElaborare = await eventiRicevuti.ListDaElaborareAsync(strutturaId, DateTime.UtcNow, cancellationToken);
        var controlloDovuto = integrazione.AvvisiDiretti
            && (!UltimoControllo.TryGetValue(strutturaId, out var ultimo) || DateTime.UtcNow - ultimo >= IntervalloControllo);
        if (daElaborare.Count == 0 && !controlloDovuto)
        {
            return;
        }

        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        var canali = await wubookClient.GetChannelsInfoAsync(token, cancellationToken);
        string NomeCanale(WubookPrenotazione b) => canali.FirstOrDefault(c => c.Id == b.IdChannel)?.Nome ?? "Sito Web";

        foreach (var evento in daElaborare)
        {
            await ElaboraEventoAsync(evento, token, lcode, NomeCanale, cancellationToken);
        }

        if (controlloDovuto)
        {
            // Segnato prima: se il controllo fallisce si riprova al prossimo intervallo, non al prossimo minuto.
            UltimoControllo[strutturaId] = DateTime.UtcNow;
            await ControllaPrenotazioniRecentiAsync(strutturaId, token, lcode, NomeCanale, cancellationToken);
        }
    }

    private async Task ElaboraEventoAsync(WubookEventoRicevuto evento, string token, string lcode, Func<WubookPrenotazione, string> nomeCanale, CancellationToken cancellationToken)
    {
        // Presa in carico prima di scaricare: un avviso della stessa prenotazione che arriva da qui in
        // poi rimette il segno, e al prossimo giro la prenotazione si riscarica con la modifica.
        if (!await eventiRicevuti.PrendiInCaricoAsync(evento.Id, cancellationToken))
        {
            return;
        }

        string? errore;
        try
        {
            var booking = await wubookClient.FetchBookingAsync(token, lcode, evento.Rcode, cancellationToken);
            if (booking is null)
            {
                errore = "Impossibile recuperare la prenotazione dall'OTA (fetch_booking).";
            }
            else
            {
                await prenotazioniService.ImportaBookingRicevutoAsync(evento.StrutturaId, booking, nomeCanale(booking), cancellationToken);
                errore = null;
            }
        }
        catch (Exception ex)
        {
            errore = ex.Message;
        }

        var adesso = DateTime.UtcNow;
        var tentativi = errore is null ? 0 : evento.Tentativi + 1;
        var riprova = errore is not null && tentativi < TentativiMassimi;
        await eventiRicevuti.RegistraEsitoAsync(evento.Id, errore, tentativi, riprova ? adesso + AttesaDopo(tentativi) : null, adesso, cancellationToken);

        // Nel Log al quinto tentativo (si continua a riprovare) e quando si smette: prima è un errore
        // che può ancora risolversi da solo.
        if (errore is not null && (tentativi == TentativiPrimaDellAvviso || !riprova))
        {
            var esito = riprova
                ? $"non ancora importata dopo {tentativi} tentativi, si continua a riprovare per circa 24 ore"
                : $"NON importata dopo {tentativi} tentativi, non si riprova più: va inserita a mano";
            await logEventi.RegistraAsync(
                riprova ? LivelloLog.Warning : LivelloLog.Error,
                $"Prenotazione OTA rcode={evento.Rcode} {esito}. Ultimo errore: {errore}",
                origine: "Wubook",
                clienteId: await strutture.GetClienteIdAsync(evento.StrutturaId, cancellationToken),
                strutturaId: evento.StrutturaId,
                categoria: "Wubook",
                cancellationToken: cancellationToken);
        }
    }

    private async Task ControllaPrenotazioniRecentiAsync(Guid strutturaId, string token, string lcode, Func<WubookPrenotazione, string> nomeCanale, CancellationToken cancellationToken)
    {
        var oggi = DateTime.UtcNow.Date;
        var recenti = await wubookClient.FetchBookingsCreateAsync(token, lcode, oggi.AddDays(-GiorniControllo), oggi.AddDays(1), cancellationToken);

        foreach (var booking in recenti)
        {
            var evento = await eventiRicevuti.GetByRcodeAsync(strutturaId, booking.RCode, cancellationToken);
            // Già in coda come avviso, o già fallita fino all'ultimo tentativo: non la si riprova ogni 15 minuti.
            if (evento is { DaElaborare: true } or { ImportazioneRiuscita: false, Tentativi: >= TentativiMassimi })
            {
                continue;
            }

            try
            {
                if (await prenotazioniService.ImportaSeMancanteAsync(strutturaId, booking, nomeCanale(booking), cancellationToken))
                {
                    await logEventi.RegistraAsync(
                        LivelloLog.Warning,
                        $"Prenotazione OTA rcode={booking.RCode} recuperata dal controllo periodico: l'avviso dell'OTA non era arrivato.",
                        origine: "Wubook",
                        clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                        strutturaId: strutturaId,
                        categoria: "Wubook",
                        cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // In coda come un avviso: si riprova con le attese crescenti e l'esito finale va nel Log,
                // invece di ritentarla e segnalarla a ogni controllo.
                await eventiRicevuti.SegnaDaElaborareAsync(strutturaId, booking.RCode, lcode, DateTime.UtcNow, cancellationToken);
                await logEventi.RegistraAsync(
                    LivelloLog.Warning,
                    $"Controllo periodico: prenotazione OTA rcode={booking.RCode} mancante e non importata ({ex.Message}): messa in coda, si riprova.",
                    origine: "Wubook",
                    clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                    strutturaId: strutturaId,
                    categoria: "Wubook",
                    cancellationToken: cancellationToken);
            }
        }
    }

    /// <summary>
    /// Attiva la ricezione diretta: prima la struttura si segna come tale, poi si chiede all'OTA di
    /// mandare gli avvisi qui (con l'avviso di prova). Nell'ordine inverso un avviso arrivato in mezzo
    /// non troverebbe la struttura e andrebbe perso. Se l'OTA rifiuta, si torna a com'era.
    /// </summary>
    public async Task<StatoAvvisiDiretti> AttivaAsync(ICurrentUser currentUser, Guid strutturaId, string urlAvvisi, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? throw new ConflictException("Integrazione OTA non configurata per questa struttura.");

        var attuale = await wubookClient.PushUrlAsync(token, lcode, cancellationToken);
        if (attuale is not null && attuale != urlAvvisi)
        {
            integrazione.UrlAvvisiPrecedente = attuale;
        }

        var eraneAttivi = integrazione.AvvisiDiretti;
        integrazione.AvvisiDiretti = true;
        integrazione.UpdatedAtUtc = DateTime.UtcNow;
        await integrazioni.UpsertAsync(integrazione, cancellationToken);

        try
        {
            await wubookClient.PushActivationAsync(token, lcode, urlAvvisi, prova: true, cancellationToken);
        }
        catch
        {
            integrazione.AvvisiDiretti = eraneAttivi;
            await integrazioni.UpsertAsync(integrazione, cancellationToken);
            throw;
        }

        await RegistraNelLogAsync(strutturaId, $"Ricezione diretta delle prenotazioni OTA attivata (prima gli avvisi andavano a: {attuale ?? "nessun indirizzo"}).", currentUser, cancellationToken);
        return await StatoAsync(currentUser, strutturaId, urlAvvisi, cancellationToken);
    }

    /// <summary>
    /// Disattiva: l'OTA torna a mandare gli avvisi all'indirizzo di prima (gestisoft.it), poi la
    /// struttura torna al polling. In quest'ordine nessun avviso resta senza destinatario: quelli
    /// arrivati nel frattempo a gestisoft.it restano "non letti" e li prende il polling.
    /// </summary>
    public async Task<StatoAvvisiDiretti> DisattivaAsync(ICurrentUser currentUser, Guid strutturaId, string urlAvvisi, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? throw new ConflictException("Integrazione OTA non configurata per questa struttura.");

        var precedente = integrazione.UrlAvvisiPrecedente ?? "";
        await wubookClient.PushActivationAsync(token, lcode, precedente, prova: false, cancellationToken);

        integrazione.AvvisiDiretti = false;
        integrazione.UpdatedAtUtc = DateTime.UtcNow;
        await integrazioni.UpsertAsync(integrazione, cancellationToken);

        await RegistraNelLogAsync(strutturaId, $"Ricezione diretta delle prenotazioni OTA disattivata: gli avvisi tornano a {(precedente == "" ? "nessun indirizzo" : precedente)}.", currentUser, cancellationToken);
        return await StatoAsync(currentUser, strutturaId, urlAvvisi, cancellationToken);
    }

    /// <summary>Stato per il pannello: interruttore salvato e indirizzo che l'OTA ha davvero registrato.</summary>
    public async Task<StatoAvvisiDiretti> StatoAsync(ICurrentUser currentUser, Guid strutturaId, string? urlAvvisi, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (integrazione is null)
        {
            return new StatoAvvisiDiretti(false, false, null, null, null);
        }

        string? registrato = null;
        string? errore = null;
        try
        {
            var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
            registrato = await wubookClient.PushUrlAsync(token, lcode, cancellationToken);
        }
        catch (Exception ex)
        {
            errore = ex.Message;
        }

        var corretto = urlAvvisi is not null && registrato == urlAvvisi;
        // L'indirizzo nostro contiene il segreto: al pannello non si manda.
        return new StatoAvvisiDiretti(integrazione.AvvisiDiretti, corretto, corretto ? "questo gestionale" : registrato, integrazione.UrlAvvisiPrecedente, errore);
    }

    private async Task RegistraNelLogAsync(Guid strutturaId, string messaggio, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        await logEventi.RegistraAsync(
            LivelloLog.Info,
            messaggio,
            origine: "SuperAdmin",
            clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
            strutturaId: strutturaId,
            categoria: "Wubook",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

    private static void RichiediSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può gestire la ricezione delle prenotazioni OTA.");
        }
    }
}
