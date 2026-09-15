using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Application.Notifiche;

namespace GestiSoft.Application.Osservatorio;

public record RisultatoInvioOsservatorio(int ArriviInviati, int CheckoutInviati, int GiorniChiusi, string? Messaggio);

/// <param name="ChiusoFinoA">Giornata fino a cui l'appartamento risulta chiuso lato Osservatorio: gli arrivi anteriori non sono più trasmissibili.</param>
/// <param name="InTermine">Arrivo ancora trasmissibile: a false l'interfaccia non deve offrire l'invio (la giornata è già stata chiusa).</param>
/// <param name="AppartamentoId">Appartamento a cui la schedina appartiene, dedotto dalla tipologia della sua camera. Null se quella tipologia non è associata a nessun appartamento: in quel caso la schedina non è trasmissibile e va configurata l'associazione.</param>
public record SchedinaOsservatorio(
    Guid OspiteId,
    Guid? PrenotazioneId,
    string NomeOspite,
    string? Camera,
    DateTime? CheckIn,
    DateTime? CheckOut,
    bool ArrivoInviato,
    bool? PartenzaInviata,
    DateTime? ChiusoFinoA,
    bool InTermine,
    Guid? AppartamentoId,
    string? AppartamentoNome);

/// <param name="ChiusoFinoA">Giornata che il servizio Osservatorio indica come prossima da chiudere: è quella i cui arrivi sono ancora trasmissibili.</param>
/// <param name="Errore">Motivo per cui il dato non è stato letto; quando valorizzato, ChiusoFinoA è l'ultimo valore noto in locale (o null).</param>
public record StatoAppartamentoOsservatorio(Guid AppartamentoId, string? Nome, DateTime? ChiusoFinoA, string? Errore);

/// <summary>
/// L'Osservatorio si invia una giornata alla volta: il cursore indica il **prossimo giorno da
/// chiudere**, ed è l'unico giorno i cui arrivi sono ancora trasmissibili. Tutto ciò che lo precede
/// appartiene a una giornata già chiusa e verrebbe rifiutato ("Invalid Date") — esempio dato
/// dall'utente: schedina dell'08/08 con chiusura al 09/08, da non inviare.
///
/// Casi limite, tutti risolti verso il "non trasmissibile" perché un pulsante che non c'è è meglio
/// di un invio che il servizio respinge:
/// - cursore non valorizzato (appartamento mai chiuso): si assume oggi, quindi valgono solo gli
///   arrivi odierni — prima si consideravano trasmissibili gli arrivi di qualsiasi giorno passato,
///   ed è il motivo per cui il pulsante "Invia" compariva anche su schedine vecchie;
/// - arretrato da recuperare (cursore indietro rispetto a oggi): niente è trasmissibile a mano, le
///   giornate arretrate le chiude solo il job automatico (vedi ProcessaAppartamentoAsync);
/// - giornata odierna già chiusa (cursore a domani): non c'è più niente da inviare per oggi.
/// </summary>
public static class TerminiOsservatorio
{
    /// <summary>Giorno i cui arrivi sono trasmissibili adesso, o null se in questo momento non lo è nessuno.</summary>
    public static DateTime? GiornoTrasmissibile(DateTime? cursore, DateTime oggi)
    {
        var prossimoDaChiudere = cursore?.Date ?? oggi.Date;
        return prossimoDaChiudere < oggi.Date ? null : prossimoDaChiudere;
    }

    public static bool IsInTermine(DateTime? checkIn, DateTime? cursore, DateTime oggi) =>
        checkIn is { } arrivo && GiornoTrasmissibile(cursore, oggi) is { } giorno && arrivo.Date == giorno;
}

/// <summary>
/// Invio giornaliero all'Osservatorio Turistico — porta SendSchedinaOseervatorio/CloseDay di
/// StatePoliceLogic del legacy: login, recupero di eventuali giorni arretrati non chiusi (solo
/// checkout + enddayfrompms, niente nuovi arrivi per giorni passati), poi per il giorno corrente
/// invio degli arrivi e chiusura giornata, logout.
/// A differenza del legacy: (1) il cursore di chiusura è per Appartamento invece che globale
/// all'installazione — vedi <see cref="OsservatorioAppartamento"/>; (2) se enddayfrompms fallisce
/// il cursore NON avanza (il legacy lo avanzava comunque, desincronizzandosi dal server — bug
/// trovato leggendo il codice, non solo testando dal vivo); (3) gli arrivi/checkout di un intero
/// appartamento in un giorno vengono raggruppati in una sola chiamata invece che una per ospite
/// (batching esplicitamente autorizzato dall'utente per questa fase, non verificato contro un
/// endpoint reale); (4) enddayfrompms (chiusura giornata) avviene SOLO dal job automatico
/// (<see cref="InviaSistemaAsync"/>), mai dall'invio manuale (<see cref="InviaOraAsync"/>, vedi
/// il parametro <c>automatico</c> di ProcessaAppartamentoAsync) — su richiesta esplicita
/// dell'utente: è un'operazione irreversibile lato Osservatorio (avanza il loro cursore) e non
/// deve scattare solo perché l'operatore preme "Invia ora" per verificare che l'invio funzioni.
/// </summary>
public class OsservatorioInvioService(
    IOspiteRepository ospiti,
    IPrenotazioneRepository prenotazioni,
    IOsservatorioAppartamentoRepository appartamenti,
    IOsservatorioInvioRepository invii,
    IAnagraficaAlloggiatiWebRepository anagrafica,
    IOsservatorioClientResolver clientResolver,
    IStrutturaRepository strutture,
    PermessoStrutturaGuard permessoGuard,
    ConcessioneServiziGuard concessioneGuard,
    ILogEventoService logEventi,
    NotificaService notificaService)
{
    /// <summary>
    /// Invio manuale su richiesta esplicita dell'operatore — a differenza del job automatico, NON
    /// chiude mai la giornata (enddayfrompms): quell'operazione è irreversibile lato Osservatorio
    /// (avanza il loro cursore) e deve avvenire solo al momento previsto, non ogni volta che si
    /// preme "Invia ora" per verificare che l'invio funzioni. Manda solo gli arrivi di oggi.
    /// </summary>
    public async Task<RisultatoInvioOsservatorio> InviaOraAsync(ICurrentUser currentUser, Guid strutturaId, Guid appartamentoId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceWrite, cancellationToken);

        var appartamento = await appartamenti.GetAsync(strutturaId, appartamentoId, cancellationToken)
            ?? throw new NotFoundException("Appartamento Osservatorio Turistico non trovato.");

        return await ProcessaAppartamentoAsync(strutturaId, appartamento, automatico: false, cancellationToken, contaTentativi: false);
    }

    /// <summary>
    /// Legge dal servizio Osservatorio, appartamento per appartamento, la giornata che risulta da
    /// chiudere lato loro — l'unico dato che conta davvero per sapere cosa è ancora trasmissibile.
    /// Il cursore salvato in locale è solo una cache di ciò che abbiamo inviato noi: su una
    /// struttura mai chiusa da questo gestionale è vuoto, e mostrare "mai chiuso" sarebbe falso,
    /// perché lato Osservatorio una data di chiusura esiste comunque (anche da installazioni
    /// precedenti). Ogni lettura riallinea la cache locale, così anche l'elenco schedine si basa
    /// subito sul dato vero.
    /// È una sola lettura (login + GetCurrentStatusDate + logout): non invia niente e non chiude
    /// nessuna giornata.
    /// </summary>
    public async Task<IReadOnlyList<StatoAppartamentoOsservatorio>> LeggiStatoRemotoAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var lista = await appartamenti.ListByStrutturaAsync(strutturaId, cancellationToken);
        var stati = new List<StatoAppartamentoOsservatorio>();

        foreach (var appartamento in lista)
        {
            if (string.IsNullOrWhiteSpace(appartamento.EntityCode) || string.IsNullOrWhiteSpace(appartamento.Password) || string.IsNullOrWhiteSpace(appartamento.HotelCode))
            {
                stati.Add(new StatoAppartamentoOsservatorio(appartamento.Id, appartamento.Nome, null, "Credenziali non configurate."));
                continue;
            }

            try
            {
                var client = clientResolver.Risolvi(appartamento.Provider);
                var login = await client.LoginAsync(appartamento.EntityCode, appartamento.Password, cancellationToken);
                if (!login.Ok || login.Token is null)
                {
                    stati.Add(new StatoAppartamentoOsservatorio(appartamento.Id, appartamento.Nome, appartamento.CursoreDataAtUtc, login.Errore ?? "Login non riuscito."));
                    continue;
                }

                DateTime? statoRemoto;
                try
                {
                    statoRemoto = await client.GetCurrentStatusDateAsync(login.Token, appartamento.HotelCode!, cancellationToken);
                }
                finally
                {
                    // Logout sempre, anche se la lettura fallisce: senza questo una chiamata andata
                    // male lascerebbe la sessione aperta sul servizio esterno (stesso try/finally di
                    // OsservatorioConfigService.VerificaConnessioneAsync e di ProcessaAppartamentoAsync).
                    await client.LogoutAsync(login.Token, cancellationToken);
                }

                if (statoRemoto is { } daChiudere)
                {
                    appartamento.CursoreDataAtUtc = daChiudere.Date;
                    await appartamenti.UpdateAsync(appartamento, cancellationToken);
                }

                stati.Add(new StatoAppartamentoOsservatorio(
                    appartamento.Id,
                    appartamento.Nome,
                    statoRemoto?.Date ?? appartamento.CursoreDataAtUtc,
                    statoRemoto is null ? "Stato corrente non disponibile." : null));
            }
            catch (Exception ex)
            {
                // Una lettura fallita non deve rompere la schermata: si mostra l'ultimo valore noto
                // insieme al motivo, e restano leggibili gli altri appartamenti.
                stati.Add(new StatoAppartamentoOsservatorio(appartamento.Id, appartamento.Nome, appartamento.CursoreDataAtUtc, ex.Message));
            }
        }

        return stati;
    }

    /// <summary>
    /// Invio manuale per tutti gli appartamenti della Struttura, uno alla volta — stessa cosa che fa
    /// il job giornaliero, richiamabile a mano senza dover scegliere un appartamento (la schermata
    /// non ne fa più scegliere uno: ogni schedina sa già dove va dichiarata).
    /// </summary>
    public async Task<IReadOnlyList<RisultatoInvioOsservatorio>> InviaOraTuttiAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceWrite, cancellationToken);
        return await InviaSistemaAsync(strutturaId, cancellationToken, contaTentativi: false);
    }

    /// <summary>
    /// Invio del solo arrivo indicato, su richiesta dell'operatore dal pulsante sulla riga. Stesse
    /// regole dell'invio manuale di tutto l'appartamento (mai chiusura di giornata, che resta
    /// prerogativa del job automatico): cambia solo che parte una schedina sola invece di tutte
    /// quelle del giorno.
    /// </summary>
    public async Task<RisultatoInvioOsservatorio> InviaSingoloArrivoAsync(ICurrentUser currentUser, Guid strutturaId, Guid ospiteId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceWrite, cancellationToken);

        var ospite = await ospiti.GetConPrenotazioneAsync(strutturaId, ospiteId, cancellationToken)
            ?? throw new NotFoundException("Ospite non trovato.");

        if (ospite.Prenotazione is not { } prenotazione)
        {
            throw new ConflictException("La scheda non è collegata a nessuna prenotazione.");
        }

        // L'appartamento non lo sceglie l'operatore: si deduce dalla tipologia della camera, che è
        // proprio ciò che stabilisce dove quell'ospite va dichiarato. Così una schedina non può
        // finire nell'appartamento sbagliato per una selezione distratta.
        var perTipologia = await RisolviAppartamentiPerTipologiaAsync(strutturaId, cancellationToken);
        var appartamento = prenotazione.Camera?.TipologiaId is { } tipologiaId && perTipologia.TryGetValue(tipologiaId, out var trovato) ? trovato : null;

        if (appartamento is null)
        {
            throw new ConflictException(
                "Nessun appartamento Osservatorio Turistico associato alla tipologia di questa camera: collegala a un appartamento in Impostazioni prima di trasmettere.");
        }

        // Giornata già chiusa lato Osservatorio: l'arrivo non è più trasmissibile e il servizio lo
        // rifiuterebbe. Si dice esplicitamente perché, senza questo controllo, la ricerca più sotto
        // (che guarda solo gli arrivi di quel giorno) risponderebbe "ospite non trovato" — vero ma
        // fuorviante, visto che l'ospite esiste e il problema è la data.
        var oggi = DateTime.UtcNow.Date;
        if (!TerminiOsservatorio.IsInTermine(prenotazione.CheckIn, appartamento.CursoreDataAtUtc, oggi))
        {
            throw new ConflictException(TerminiOsservatorio.GiornoTrasmissibile(appartamento.CursoreDataAtUtc, oggi) is { } giorno
                ? $"{appartamento.Nome} è fermo al {giorno:dd/MM/yyyy}: si possono trasmettere solo gli arrivi di quel giorno, non quello del {prenotazione.CheckIn:dd/MM/yyyy}."
                : $"{appartamento.Nome} è fermo al {appartamento.CursoreDataAtUtc:dd/MM/yyyy} e ha giornate arretrate da recuperare: la chiusura avviene solo con l'invio automatico.");
        }

        return await ProcessaAppartamentoAsync(strutturaId, appartamento, automatico: false, cancellationToken, soloOspiteId: ospiteId, giornoArrivo: prenotazione.CheckIn, contaTentativi: false);
    }

    /// <summary>Usato dal job Quartz schedulato (Worker) — nessun ICurrentUser, un appartamento alla volta: un fallimento su uno non blocca gli altri.</summary>
    public async Task<IReadOnlyList<RisultatoInvioOsservatorio>> InviaSistemaAsync(Guid strutturaId, CancellationToken cancellationToken, bool contaTentativi = true)
    {
        var lista = await appartamenti.ListByStrutturaAsync(strutturaId, cancellationToken);
        if (lista.Count == 0)
        {
            await logEventi.RegistraAsync(
                LivelloLog.Info,
                "Invio Osservatorio Turistico: 0 arrivi e 0 partenze inviati — nessun appartamento configurato.",
                origine: "Osservatorio",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "Osservatorio",
                cancellationToken: cancellationToken);
            return [];
        }

        var risultati = new List<RisultatoInvioOsservatorio>();

        foreach (var appartamento in lista)
        {
            try
            {
                risultati.Add(await ProcessaAppartamentoAsync(strutturaId, appartamento, automatico: true, cancellationToken, contaTentativi: contaTentativi));
            }
            catch (Exception ex)
            {
                await SalvaErroreAsync(appartamento, ex.Message, contaTentativi ? EsitoTentativo.ErroreRitentabile : null, cancellationToken);
                risultati.Add(new RisultatoInvioOsservatorio(0, 0, 0, ex.Message));
            }
        }

        return risultati;
    }

    /// <summary>
    /// Elenco arrivi/partenze dell'anno indicato di un appartamento per la schermata operativa —
    /// da inviare e già inviati. La partenza si considera inviata se il cursore di chiusura
    /// giornata dell'appartamento ha già superato la data di check-out (nessun flag dedicato per
    /// singola prenotazione: il checkout viene chiuso per giorno, non per ospite).
    /// </summary>
    /// <summary>
    /// Elenco arrivi/partenze dell'anno per l'intera Struttura, non per singolo appartamento:
    /// l'appartamento di ogni riga viene dedotto dalla tipologia della sua camera (associazione
    /// univoca, vedi RisolviAppartamentiPerTipologiaAsync), così l'operatore vede tutte le schedine
    /// in un elenco solo — riconoscendole dalla colonna Camera — invece di doverle cercare
    /// appartamento per appartamento. Le date di chiusura e la trasmissibilità sono quelle
    /// dell'appartamento di ciascuna riga, non di uno selezionato a parte.
    /// </summary>
    public async Task<IReadOnlyList<SchedinaOsservatorio>> ListSchedineAsync(ICurrentUser currentUser, Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var perTipologia = await RisolviAppartamentiPerTipologiaAsync(strutturaId, cancellationToken);
        var recenti = await ospiti.ListRecentiOsservatorioAsync(strutturaId, tipologieIds: null, anno, cancellationToken);
        var oggi = DateTime.UtcNow.Date;

        return recenti
            // Chi non ha un appartamento associato non va dichiarato da nessuna parte: la riga non
            // compare affatto, invece di restare in elenco come promemoria di qualcosa da fare
            // (scelta esplicita dell'utente: le tipologie non assegnate sono tali per volontà).
            .Where(o => AppartamentoDi(o, perTipologia) is not null)
            .Select(o =>
        {
            var appartamento = AppartamentoDi(o, perTipologia);

            return new SchedinaOsservatorio(
                o.Id,
                o.PrenotazioneId,
                $"{o.Cognome} {o.Nome}".Trim(),
                o.Prenotazione?.Camera?.Nome,
                o.Prenotazione?.CheckIn,
                o.Prenotazione?.CheckOut,
                o.Prenotazione?.PMS ?? false,
                o.Prenotazione?.CheckOut is { } checkOut ? appartamento?.CursoreDataAtUtc?.Date > checkOut.Date : null,
                appartamento?.CursoreDataAtUtc,
                // Senza appartamento associato non c'è nessun posto dove dichiararla: non trasmissibile
                // finché qualcuno non collega quella tipologia a un appartamento in Impostazioni.
                appartamento is not null && TerminiOsservatorio.IsInTermine(o.Prenotazione?.CheckIn, appartamento.CursoreDataAtUtc, oggi),
                appartamento?.Id,
                appartamento?.Nome);
        }).ToList();
    }

    /// <summary>Mappa TipologiaId → appartamento che la dichiara. L'associazione è univoca (una tipologia appartiene a un solo appartamento), quindi la scelta non è mai ambigua.</summary>
    private async Task<Dictionary<Guid, OsservatorioAppartamento>> RisolviAppartamentiPerTipologiaAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var lista = await appartamenti.ListByStrutturaAsync(strutturaId, cancellationToken);

        return lista
            .SelectMany(a => a.Tipologie.Select(t => (t.TipologiaId, Appartamento: a)))
            .GroupBy(x => x.TipologiaId)
            .ToDictionary(g => g.Key, g => g.First().Appartamento);
    }

    private static OsservatorioAppartamento? AppartamentoDi(Ospite ospite, Dictionary<Guid, OsservatorioAppartamento> perTipologia) =>
        ospite.Prenotazione?.Camera?.TipologiaId is { } tipologiaId && perTipologia.TryGetValue(tipologiaId, out var appartamento)
            ? appartamento
            : null;

    /// <summary>Anni con almeno una prenotazione per il selettore Anno della schermata operativa — su richiesta esplicita, non deve proporre anni sicuramente vuoti.</summary>
    public async Task<IReadOnlyList<int>> ListaAnniAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);
        return await prenotazioni.ListaAnniConPrenotazioniAsync(strutturaId, cancellationToken);
    }

    // contaTentativi è separato da automatico di proposito: "Invia ora" passa dal percorso automatico
    // (deve poter chiudere la giornata come il job), ma non deve consumare i tentativi serali —
    // altrimenti un paio di tentativi a mano andati male zittirebbero il job di quella sera.
    private async Task<RisultatoInvioOsservatorio> ProcessaAppartamentoAsync(Guid strutturaId, OsservatorioAppartamento appartamento, bool automatico, CancellationToken cancellationToken, Guid? soloOspiteId = null, DateTime? giornoArrivo = null, bool contaTentativi = true)
    {
        await concessioneGuard.EnsureOsservatorioAsync(strutturaId, cancellationToken);

        var adesso = DateTime.UtcNow;
        var oggi = adesso.Date;

        // null quando l'invio è partito a mano: i tentativi contano solo per il job serale, un
        // tentativo manuale andato male non deve zittirlo.
        EsitoTentativo? Tentativo(EsitoTentativo esito) => contaTentativi ? esito : null;

        // Tentativi della giornata esauriti, o attesa dopo l'ultimo fallimento non ancora trascorsa:
        // si riprende domani all'orario configurato. Vale solo per il job — un invio chiesto a mano
        // dall'operatore parte comunque, ed è il suo modo di riprovare senza aspettare.
        //
        // Va **prima** dei controlli di configurazione qui sotto, non dopo: sono loro il caso che si
        // ripete identico ad ogni giro (un appartamento senza credenziali resta senza credenziali),
        // e lasciarli davanti al limite significherebbe continuare a scriverne l'esito ogni minuto.
        if (contaTentativi && !PoliticaTentativi.PuoTentare(appartamento, adesso))
        {
            return new RisultatoInvioOsservatorio(0, 0, 0, null);
        }

        if (string.IsNullOrWhiteSpace(appartamento.EntityCode) || string.IsNullOrWhiteSpace(appartamento.Password) || string.IsNullOrWhiteSpace(appartamento.HotelCode))
        {
            await SalvaErroreAsync(appartamento, "Credenziali Osservatorio Turistico non configurate.", Tentativo(EsitoTentativo.ErroreDefinitivo), cancellationToken);
            return new RisultatoInvioOsservatorio(0, 0, 0, "Credenziali non configurate.");
        }

        if (appartamento.Tipologie.Count == 0)
        {
            await SalvaErroreAsync(appartamento, "Nessuna tipologia camera associata a questo appartamento.", Tentativo(EsitoTentativo.ErroreDefinitivo), cancellationToken);
            return new RisultatoInvioOsservatorio(0, 0, 0, "Nessuna tipologia camera associata.");
        }

        // Giornata di oggi già chiusa con successo (cursore già avanzato a domani): niente da fare,
        // evita un login/logout inutile verso il servizio esterno ad ogni giro del job (ogni minuto).
        if (appartamento.CursoreDataAtUtc?.Date > oggi)
        {
            return new RisultatoInvioOsservatorio(0, 0, 0, null);
        }

        var client = clientResolver.Risolvi(appartamento.Provider);
        var login = await client.LoginAsync(appartamento.EntityCode, appartamento.Password, cancellationToken);
        if (!login.Ok || login.Token is null)
        {
            await SalvaErroreAsync(appartamento, login.Errore ?? "Login non riuscito.", Tentativo(EsitoTentativo.ErroreRitentabile), cancellationToken);
            return new RisultatoInvioOsservatorio(0, 0, 0, login.Errore);
        }

        // Il cursore autorevole è quello del server Osservatorio, non il nostro locale: ogni
        // struttura può essere "ferma" a un giorno diverso lato loro (es. per un'installazione
        // precedente mai proseguita, come scoperto per Villa Chifeci Scopello — ferma al 1°
        // maggio mentre da noi il cursore locale era vuoto). GetCurrentStatusDate restituisce
        // già il PROSSIMO giorno da chiudere (non l'ultimo chiuso: un +1 aggiuntivo veniva
        // rifiutato con "Invalid Date", verificato dal vivo chiudendo per intero l'arretrato di
        // Villa Chifeci Scopello, 120 giorni, fino ad oggi senza errori). Il cursore locale
        // (CursoreDataAtUtc) resta solo come cache/storico per la UI (v. ListSchedineAsync), qui
        // viene sempre risincronizzato da GetCurrentStatusDate.
        var statoRemoto = await client.GetCurrentStatusDateAsync(login.Token, appartamento.HotelCode!, cancellationToken);
        if (statoRemoto is null)
        {
            await SalvaErroreAsync(appartamento, "Impossibile leggere lo stato corrente (GetCurrentStatusDate) dall'Osservatorio Turistico.", Tentativo(EsitoTentativo.ErroreRitentabile), cancellationToken);
            return new RisultatoInvioOsservatorio(0, 0, 0, "Stato remoto non disponibile.");
        }

        var tipologieIds = appartamento.Tipologie.Select(t => t.TipologiaId).ToHashSet();
        var cursore = statoRemoto.Value.Date;

        // Allinea subito la cache locale a quello che dice il servizio, anche quando non ci sarà
        // nulla da inviare. Senza questo, un appartamento la cui giornata risulta già chiusa lato
        // Osservatorio (cursore remoto oltre oggi) restava indietro col cursore locale, il gate qui
        // sopra non scattava mai e il job rifaceva login, lettura stato e log **ogni minuto** per
        // tutta l'ora tra l'orario di invio e la mezzanotte — centinaia di righe di log identiche
        // a sera e altrettante chiamate inutili al servizio della PA. Va allineata anche
        // all'indietro: se il portale è fermo a una data precedente, il nostro "chiuso fino al"
        // mostrato in pagina sarebbe altrimenti una data falsa.
        if (appartamento.CursoreDataAtUtc?.Date != cursore)
        {
            appartamento.CursoreDataAtUtc = cursore;
            await appartamenti.UpdateAsync(appartamento, cancellationToken);
        }

        int arriviInviati = 0, checkoutInviati = 0, giorniChiusi = 0;

        try
        {
            if (!automatico)
            {
                // Invio manuale: mai enddayfrompms, né per l'arretrato né per oggi — solo il job
                // schedulato (automatico: true) può chiudere una giornata, sempre e solo all'orario
                // configurato (OraInvioGiornaliero). Se c'è arretrato da chiudere, va lasciato al
                // job: qui si mandano solo gli arrivi di oggi, il cursore non si tocca.
                if (cursore < oggi)
                {
                    return new RisultatoInvioOsservatorio(0, 0, 0,
                        $"Ci sono giornate non ancora chiuse (da {cursore:dd/MM/yyyy}) — la chiusura avviene solo automaticamente all'orario configurato, non con l'invio manuale.");
                }

                arriviInviati = await InviaArriviAsync(client, strutturaId, appartamento, login.Token, (giornoArrivo ?? oggi).Date, tipologieIds, cancellationToken, soloOspiteId);

                appartamento.UltimoInvioAtUtc = DateTime.UtcNow;
                appartamento.UltimeSchedineInviate = arriviInviati;
                appartamento.UltimoErrore = null;
                await appartamenti.UpdateAsync(appartamento, cancellationToken);

                await logEventi.RegistraAsync(
                    LivelloLog.Info,
                    $"Invio manuale Osservatorio Turistico ({appartamento.Nome}): {arriviInviati} arriv{(arriviInviati == 1 ? "o" : "i")} inviat{(arriviInviati == 1 ? "o" : "i")}{(soloOspiteId is null ? string.Empty : " (invio singolo)")} — nessuna chiusura giornata (solo il job automatico chiude).",
                    origine: "Osservatorio",
                    clienteId: await strutture.GetClienteIdAsync(appartamento.StrutturaId, cancellationToken),
                    strutturaId: appartamento.StrutturaId,
                    categoria: "Osservatorio",
                    cancellationToken: cancellationToken);

                return new RisultatoInvioOsservatorio(arriviInviati, 0, 0, null);
            }

            // Recupero arretrati: solo checkout + chiusura giornata, niente nuovi arrivi per giorni
            // passati (fedele al legacy — gli arrivi si inviano solo per il giorno corrente).
            while (cursore < oggi)
            {
                checkoutInviati += await ChiudiGiornataAsync(client, strutturaId, appartamento, login.Token, cursore, tipologieIds, cancellationToken);
                giorniChiusi++;
                cursore = cursore.AddDays(1);
                appartamento.CursoreDataAtUtc = cursore;
                await appartamenti.UpdateAsync(appartamento, cancellationToken);
            }

            if (cursore == oggi)
            {
                arriviInviati = await InviaArriviAsync(client, strutturaId, appartamento, login.Token, oggi, tipologieIds, cancellationToken);
                checkoutInviati += await ChiudiGiornataAsync(client, strutturaId, appartamento, login.Token, oggi, tipologieIds, cancellationToken);
                giorniChiusi++;
                appartamento.CursoreDataAtUtc = oggi.AddDays(1);
            }

            appartamento.UltimoInvioAtUtc = DateTime.UtcNow;
            appartamento.UltimeSchedineInviate = arriviInviati;
            appartamento.UltimoErrore = null;
            if (contaTentativi)
            {
                PoliticaTentativi.RegistraEsito(appartamento, EsitoTentativo.Riuscito, adesso);
            }

            await appartamenti.UpdateAsync(appartamento, cancellationToken);

            // Niente denominatore "X/Y" qui a differenza di Alloggiati Web/PayTourist: un invio
            // arrivi/checkout è tutto-o-niente (un rifiuto del server fa fallire l'intera chiamata,
            // vedi InviaArriviAsync/ChiudiGiornataAsync), non c'è un conteggio di "scartati" per
            // singolo ospite. Si logga ogni giornata **effettivamente processata**, anche quando
            // chiude con "0 e 0" (nessun arrivo/partenza quel giorno): serve a verificare dalla
            // pagina Log che il job gira. Non si logga invece il giro in cui non c'era proprio
            // nulla da chiudere: ripetuto ogni minuto fino a mezzanotte, era solo rumore che
            // copriva le righe vere.
            if (giorniChiusi == 0)
            {
                return new RisultatoInvioOsservatorio(arriviInviati, checkoutInviati, giorniChiusi, null);
            }

            await logEventi.RegistraAsync(
                LivelloLog.Info,
                $"Invio Osservatorio Turistico ({appartamento.Nome}): {arriviInviati} arrivi e {checkoutInviati} partenze inviati — chiuso fino al {appartamento.CursoreDataAtUtc:dd/MM/yyyy}.",
                origine: "Osservatorio",
                clienteId: await strutture.GetClienteIdAsync(appartamento.StrutturaId, cancellationToken),
                strutturaId: appartamento.StrutturaId,
                categoria: "Osservatorio",
                cancellationToken: cancellationToken);

            return new RisultatoInvioOsservatorio(arriviInviati, checkoutInviati, giorniChiusi, null);
        }
        catch (Exception ex)
        {
            // Il cursore persistito resta fermo all'ultimo giorno chiuso con successo (vedi sopra,
            // salvato incrementalmente ad ogni giorno di arretrato e solo a fine giornata corrente):
            // un fallimento su un giorno non lo fa mai avanzare oltre, cosa che il legacy invece
            // faceva (vedi doc della classe) — il prossimo giro ritenta lo stesso giorno.
            await SalvaErroreAsync(appartamento, ex.Message, Tentativo(EsitoTentativo.ErroreRitentabile), cancellationToken);
            return new RisultatoInvioOsservatorio(arriviInviati, checkoutInviati, giorniChiusi, ex.Message);
        }
        finally
        {
            await client.LogoutAsync(login.Token, cancellationToken);
        }
    }

    private async Task<int> InviaArriviAsync(IOsservatorioClient client, Guid strutturaId, OsservatorioAppartamento appartamento, string token, DateTime giorno, IReadOnlyCollection<Guid> tipologieIds, CancellationToken cancellationToken, Guid? soloOspiteId = null)
    {
        var arrivi = await ospiti.ListArriviOsservatorioAsync(strutturaId, tipologieIds, giorno, cancellationToken);

        // Invio della singola schedina: si riusa esattamente la stessa costruzione dello stay usata
        // per il giorno intero (stayId progressivo, guestId, righe salvate), filtrando l'elenco a un
        // solo ospite — così una riga inviata a mano è indistinguibile da una inviata dal batch.
        if (soloOspiteId is { } ospiteId)
        {
            arrivi = arrivi.Where(o => o.Id == ospiteId).ToList();
            if (arrivi.Count == 0)
            {
                throw new NotFoundException("Ospite non trovato tra gli arrivi di oggi ancora da inviare per questo appartamento.");
            }
        }

        if (arrivi.Count == 0)
        {
            return 0;
        }

        var builder = await CreaBuilderAsync(cancellationToken);
        var righeDaSalvare = new List<OsservatorioInvio>();
        var stays = new List<OsservatorioStayDto>();

        foreach (var ospite in arrivi)
        {
            var prenotazioneId = ospite.PrenotazioneId ?? Guid.Empty;
            var stayId = $"{giorno.Year}_{appartamento.ProssimoStayIdProgressivo:D5}";
            appartamento.ProssimoStayIdProgressivo++;

            var guestIdCapofamiglia = StayBuilderOsservatorio.GeneraGuestId(ospite.Cognome, ospite.Nome, ospite.DataNascita, prenotazioneId);
            var guestIdMembri = ospite.Membri.ToDictionary(
                m => m.Id,
                m => StayBuilderOsservatorio.GeneraGuestId(m.Cognome, m.Nome, m.DataNascita, prenotazioneId));

            stays.Add(builder.CostruisciArrivo(ospite, stayId, guestIdCapofamiglia, guestIdMembri));

            righeDaSalvare.Add(new OsservatorioInvio { StrutturaId = strutturaId, PrenotazioneId = prenotazioneId, OspiteRigaId = null, StayId = stayId, GuestId = guestIdCapofamiglia, DataInvioUtc = DateTime.UtcNow });
            foreach (var (membroId, guestId) in guestIdMembri)
            {
                righeDaSalvare.Add(new OsservatorioInvio { StrutturaId = strutturaId, PrenotazioneId = prenotazioneId, OspiteRigaId = membroId, StayId = stayId, GuestId = guestId, DataInvioUtc = DateTime.UtcNow });
            }
        }

        var esito = await client.SendArrivalsAsync(token, appartamento.HotelCode!, stays, cancellationToken);
        if (!esito.Ok)
        {
            throw new InvalidOperationException(esito.Errore ?? "Invio arrivi rifiutato dall'Osservatorio Turistico.");
        }

        await invii.AddRangeAsync(righeDaSalvare, cancellationToken);

        foreach (var ospite in arrivi)
        {
            if (ospite.PrenotazioneId is not { } prenotazioneId)
            {
                continue;
            }

            var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken);
            if (prenotazione is null)
            {
                continue;
            }

            prenotazione.PMS = true;
            prenotazione.UpdatedAtUtc = DateTime.UtcNow;
            await prenotazioni.UpdateAsync(prenotazione, cancellationToken);
        }

        return arrivi.Count;
    }

    private async Task<int> ChiudiGiornataAsync(IOsservatorioClient client, Guid strutturaId, OsservatorioAppartamento appartamento, string token, DateTime giorno, IReadOnlyCollection<Guid> tipologieIds, CancellationToken cancellationToken)
    {
        var checkout = await ospiti.ListCheckoutOsservatorioAsync(strutturaId, tipologieIds, giorno, cancellationToken);
        var inviati = 0;

        if (checkout.Count > 0)
        {
            var builder = await CreaBuilderAsync(cancellationToken);
            var stays = new List<OsservatorioStayDto>();

            foreach (var ospite in checkout)
            {
                if (ospite.PrenotazioneId is not { } prenotazioneId)
                {
                    continue;
                }

                var storico = await invii.ListByPrenotazioneAsync(prenotazioneId, cancellationToken);
                var invioCapofamiglia = storico.FirstOrDefault(s => s.OspiteRigaId == null);
                if (invioCapofamiglia is null)
                {
                    // Nessun arrivo registrato per questa prenotazione presso l'Osservatorio: non
                    // si può generare un checkout coerente (GuestId/StayId mancanti). Il legacy in
                    // questo caso avrebbe comunque inviato un GuestId nullo — validazione aggiunta
                    // qui, assente nel legacy.
                    continue;
                }

                var guestIdMembri = ospite.Membri
                    .Select(m => (m.Id, GuestId: storico.FirstOrDefault(s => s.OspiteRigaId == m.Id)?.GuestId))
                    .Where(x => x.GuestId is not null)
                    .ToDictionary(x => x.Id, x => x.GuestId!);

                stays.Add(builder.CostruisciCheckout(ospite, invioCapofamiglia.StayId, invioCapofamiglia.GuestId, guestIdMembri));
            }

            if (stays.Count > 0)
            {
                var esito = await client.SendCheckoutsAsync(token, appartamento.HotelCode!, stays, cancellationToken);
                if (!esito.Ok)
                {
                    throw new InvalidOperationException(esito.Errore ?? "Invio checkout rifiutato dall'Osservatorio Turistico.");
                }

                inviati = stays.Count;
            }
        }

        var chiusura = await client.EndDayAsync(token, appartamento.HotelCode!, giorno, cancellationToken);
        if (!chiusura.Ok)
        {
            throw new InvalidOperationException(chiusura.Errore ?? $"Chiusura giornata {giorno:dd/MM/yyyy} rifiutata dall'Osservatorio Turistico.");
        }

        return inviati;
    }

    private async Task<StayBuilderOsservatorio> CreaBuilderAsync(CancellationToken cancellationToken) => new(
        await anagrafica.ListLuoghiAsync(cancellationToken),
        await anagrafica.ListTipiAlloggiatoAsync(cancellationToken));

    private async Task SalvaErroreAsync(
        OsservatorioAppartamento appartamento,
        string errore,
        // null = invio partito a mano: non consuma i tentativi del job e va sempre a log.
        EsitoTentativo? esitoTentativo,
        CancellationToken cancellationToken)
    {
        var adesso = DateTime.UtcNow;
        var tentativiEsauriti = esitoTentativo is { } esito && PoliticaTentativi.RegistraEsito(appartamento, esito, adesso);

        appartamento.UltimoErrore = errore;
        appartamento.UltimoInvioAtUtc = adesso;
        await appartamenti.UpdateAsync(appartamento, cancellationToken);

        // Un errore si annota **all'ultimo tentativo utile**, non ad ogni giro: il job riprova ogni
        // minuto fino a mezzanotte, e una riga per tentativo è esattamente il rumore che il limite
        // serve a togliere. Chi legge il Log trova una riga sola, che dice quanti tentativi sono
        // stati spesi e che si riprende domani.
        if (esitoTentativo is { } e && e != EsitoTentativo.Riuscito && !tentativiEsauriti)
        {
            return;
        }

        var resa = esitoTentativo is null
            ? string.Empty
            : $" ({(appartamento.TentativiFallitiOggi == 1 ? "1 tentativo" : $"{appartamento.TentativiFallitiOggi} tentativi falliti")}; nuovo tentativo domani all'orario configurato)";

        if (tentativiEsauriti)
        {
            await SegnalaResaAsync(appartamento.StrutturaId, "Osservatorio Turistico", appartamento.Nome, errore, cancellationToken);
        }

        await logEventi.RegistraAsync(
            LivelloLog.Warning,
            $"Invio Osservatorio Turistico ({appartamento.Nome}): {errore}{resa}.",
            origine: "Osservatorio",
            clienteId: await strutture.GetClienteIdAsync(appartamento.StrutturaId, cancellationToken),
            strutturaId: appartamento.StrutturaId,
            categoria: "Osservatorio",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Una notifica al giorno per elemento quando l'invio automatico si arrende: il Log da solo non
    /// basta, perché nessuno lo apre finché non sospetta già un problema — e una configurazione
    /// mancante resterebbe tale per giorni senza che nessuno se ne accorga.
    /// </summary>
    private Task SegnalaResaAsync(Guid strutturaId, string servizio, string? nomeElemento, string errore, CancellationToken cancellationToken) =>
        notificaService.CreaSeNonEsisteAsync(
            strutturaId,
            TipoNotifica.InvioSchedineNonRiuscito,
            $"invio-non-riuscito:{servizio}:{nomeElemento}:{DateTime.UtcNow:yyyyMMdd}",
            $"{servizio}: invio non riuscito",
            $"{(string.IsNullOrWhiteSpace(nomeElemento) ? servizio : nomeElemento)}: {errore} Nuovo tentativo domani all'orario configurato.",
            cancellationToken);
}
