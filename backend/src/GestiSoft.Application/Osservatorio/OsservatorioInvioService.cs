using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

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

/// <summary>
/// Un arrivo si può ancora trasmettere solo se la sua giornata non è già stata chiusa
/// sull'Osservatorio: il cursore indica il prossimo giorno da chiudere, quindi un arrivo con data
/// anteriore appartiene a una giornata già chiusa e verrebbe rifiutato ("Invalid Date"). Esempio
/// dato dall'utente: schedina dell'08/08, chiusura al 09/08 → non va inviata.
/// Cursore non ancora valorizzato (appartamento mai chiuso) = nessuna giornata chiusa, tutto
/// trasmissibile.
/// </summary>
public static class TerminiOsservatorio
{
    public static bool IsInTermine(DateTime? checkIn, DateTime? cursore) =>
        checkIn is { } arrivo && (cursore is not { } chiuso || arrivo.Date >= chiuso.Date);
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
    ILogEventoService logEventi)
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

        return await ProcessaAppartamentoAsync(strutturaId, appartamento, automatico: false, cancellationToken);
    }

    /// <summary>
    /// Invio manuale per tutti gli appartamenti della Struttura, uno alla volta — stessa cosa che fa
    /// il job giornaliero, richiamabile a mano senza dover scegliere un appartamento (la schermata
    /// non ne fa più scegliere uno: ogni schedina sa già dove va dichiarata).
    /// </summary>
    public async Task<IReadOnlyList<RisultatoInvioOsservatorio>> InviaOraTuttiAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceWrite, cancellationToken);
        return await InviaSistemaAsync(strutturaId, cancellationToken);
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
        if (!TerminiOsservatorio.IsInTermine(prenotazione.CheckIn, appartamento.CursoreDataAtUtc))
        {
            throw new ConflictException(
                $"Giornata già chiusa sull'Osservatorio Turistico (chiuso fino al {appartamento.CursoreDataAtUtc:dd/MM/yyyy}): l'arrivo del {prenotazione.CheckIn:dd/MM/yyyy} non è più trasmissibile.");
        }

        return await ProcessaAppartamentoAsync(strutturaId, appartamento, automatico: false, cancellationToken, soloOspiteId: ospiteId, giornoArrivo: prenotazione.CheckIn);
    }

    /// <summary>Usato dal job Quartz schedulato (Worker) — nessun ICurrentUser, un appartamento alla volta: un fallimento su uno non blocca gli altri.</summary>
    public async Task<IReadOnlyList<RisultatoInvioOsservatorio>> InviaSistemaAsync(Guid strutturaId, CancellationToken cancellationToken)
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
                risultati.Add(await ProcessaAppartamentoAsync(strutturaId, appartamento, automatico: true, cancellationToken));
            }
            catch (Exception ex)
            {
                await SalvaErroreAsync(appartamento, ex.Message, cancellationToken);
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
                appartamento is not null && TerminiOsservatorio.IsInTermine(o.Prenotazione?.CheckIn, appartamento.CursoreDataAtUtc),
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

    private async Task<RisultatoInvioOsservatorio> ProcessaAppartamentoAsync(Guid strutturaId, OsservatorioAppartamento appartamento, bool automatico, CancellationToken cancellationToken, Guid? soloOspiteId = null, DateTime? giornoArrivo = null)
    {
        await concessioneGuard.EnsureOsservatorioAsync(strutturaId, cancellationToken);

        if (string.IsNullOrWhiteSpace(appartamento.EntityCode) || string.IsNullOrWhiteSpace(appartamento.Password) || string.IsNullOrWhiteSpace(appartamento.HotelCode))
        {
            await SalvaErroreAsync(appartamento, "Credenziali Osservatorio Turistico non configurate.", cancellationToken);
            return new RisultatoInvioOsservatorio(0, 0, 0, "Credenziali non configurate.");
        }

        if (appartamento.Tipologie.Count == 0)
        {
            await SalvaErroreAsync(appartamento, "Nessuna tipologia camera associata a questo appartamento.", cancellationToken);
            return new RisultatoInvioOsservatorio(0, 0, 0, "Nessuna tipologia camera associata.");
        }

        var oggi = DateTime.UtcNow.Date;

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
            await SalvaErroreAsync(appartamento, login.Errore ?? "Login non riuscito.", cancellationToken);
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
            await SalvaErroreAsync(appartamento, "Impossibile leggere lo stato corrente (GetCurrentStatusDate) dall'Osservatorio Turistico.", cancellationToken);
            return new RisultatoInvioOsservatorio(0, 0, 0, "Stato remoto non disponibile.");
        }

        var tipologieIds = appartamento.Tipologie.Select(t => t.TipologiaId).ToHashSet();
        var cursore = statoRemoto.Value.Date;

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
            await appartamenti.UpdateAsync(appartamento, cancellationToken);

            // Niente denominatore "X/Y" qui a differenza di Alloggiati Web/PayTourist: un invio
            // arrivi/checkout è tutto-o-niente (un rifiuto del server fa fallire l'intera chiamata,
            // vedi InviaArriviAsync/ChiudiGiornataAsync), non c'è un conteggio di "scartati" per
            // singolo ospite. Log ad ogni giornata effettivamente processata, anche "0 e 0" (nessun
            // arrivo/partenza oggi), con la data fino a cui risulta chiuso — l'utente deve poter
            // verificare dalla pagina Log che il job gira regolarmente, non solo quando c'è stato
            // un movimento reale.
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
            await SalvaErroreAsync(appartamento, ex.Message, cancellationToken);
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

    private async Task SalvaErroreAsync(OsservatorioAppartamento appartamento, string errore, CancellationToken cancellationToken)
    {
        appartamento.UltimoErrore = errore;
        appartamento.UltimoInvioAtUtc = DateTime.UtcNow;
        await appartamenti.UpdateAsync(appartamento, cancellationToken);

        await logEventi.RegistraAsync(
            LivelloLog.Warning,
            $"Invio Osservatorio Turistico ({appartamento.Nome}): {errore}",
            origine: "Osservatorio",
            clienteId: await strutture.GetClienteIdAsync(appartamento.StrutturaId, cancellationToken),
            strutturaId: appartamento.StrutturaId,
            categoria: "Osservatorio",
            cancellationToken: cancellationToken);
    }
}
