using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Application.Notifiche;

namespace GestiSoft.Application.PayTourist;

public record RisultatoInvioPayTourist(int Inviate, int TotalePrenotazioni, int Errori, string? Messaggio);

/// <param name="ScadenzaInvioUtc">Ultimo giorno utile per la trasmissione: 7 giorni dal check-out.</param>
/// <param name="InTermine">Ancora trasmissibile: a false l'interfaccia non deve offrire l'invio.</param>
public record PrenotazionePayTourist(
    Guid OspiteId,
    Guid? PrenotazioneId,
    string NomeOspite,
    string? Camera,
    DateTime? CheckIn,
    DateTime? CheckOut,
    bool Inviata,
    DateTime? ScadenzaInvioUtc,
    bool InTermine,
    /// <summary>Struttura PayTourist in cui la prenotazione va dichiarata, dedotta dalla tipologia della camera. Null = tipologia non associata: non trasmissibile finché non viene collegata.</summary>
    Guid? PayTouristStrutturaId,
    string? PayTouristStrutturaNome);

/// <summary>
/// Si trasmettono solo i soggiorni conclusi da non più di 7 giorni: oltre quella finestra la
/// schedina va esclusa e non più ritentata (la stessa finestra mobile che
/// IOspiteRepository.ListDaInviarePayTouristAsync applica da sempre alla selezione — qui è resa
/// esplicita per poterla mostrare anche in elenco, invece di far sparire le righe senza spiegazione).
/// </summary>
public static class TerminiPayTourist
{
    public static readonly int GiorniDalCheckOut = 7;

    public static DateTime? ScadenzaUtc(DateTime? checkOut) =>
        checkOut is { } uscita ? DateTime.SpecifyKind(uscita.Date.AddDays(GiorniDalCheckOut), DateTimeKind.Utc) : null;

    public static bool IsInTermine(DateTime? checkOut, DateTime adessoUtc) =>
        ScadenzaUtc(checkOut) is { } scadenza && adessoUtc.Date <= scadenza.Date;
}

/// <summary>
/// Invio a PayTourist — porta StatePoliceLogic.SendSchedinePayTourist del legacy: per ogni
/// "struttura" PayTourist configurata (<see cref="PayTouristStruttura"/>), recupera le prenotazioni
/// completate non ancora inviate sulle tipologie camera assegnate, le riduzioni disponibili e
/// (se richiesto) i portali online, poi invia una prenotazione alla volta (il legacy non ha mai
/// raggruppato più prenotazioni in una chiamata, fedele qui) marcando <see cref="Prenotazione.PayTourist"/>
/// solo sulle prenotazioni effettivamente accettate.
/// Due correzioni deliberate rispetto al legacy (bug trovati leggendo il codice, non solo
/// testando): (1) il legacy segnalava sempre esito complessivo "Success" anche con invii
/// parzialmente falliti — qui gli errori sono sempre riflessi in <see cref="RisultatoInvioPayTourist.Errori"/>,
/// stesso principio di sincerità già seguito da AlloggiatiWebInvioService; (2) il legacy
/// sovrascriveva (non accumulava) il messaggio d'errore ad ogni prenotazione scartata per portale
/// online mancante, perdendo tutti tranne l'ultimo — qui tutti i messaggi sono raccolti.
/// Nota: la finestra di selezione è mobile a 7 giorni sul check-out (fedele al legacy, vedi
/// IOspiteRepository.ListDaInviarePayTouristAsync) — nessun meccanismo di recupero oltre quella
/// finestra, gap noto già presente nel sistema originale, non chiuso in questa fase.
/// </summary>
public class PayTouristInvioService(
    IOspiteRepository ospiti,
    IPrenotazioneRepository prenotazioni,
    IPayTouristIntegrazioneRepository integrazioni,
    IPayTouristStrutturaRepository payTouristStrutture,
    IAnagraficaAlloggiatiWebRepository anagrafica,
    IImpostazioniStrutturaRepository impostazioniStruttura,
    IPayTouristClient client,
    IStrutturaRepository strutture,
    WubookLicenzaService wubookLicenzaService,
    PermessoStrutturaGuard permessoGuard,
    ConcessioneServiziGuard concessioneGuard,
    ILogEventoService logEventi,
    NotificaService notificaService)
{
    private record AnagraficaPayTourist(
        IReadOnlyList<VoceAnagrafica> Luoghi,
        IReadOnlyList<VoceDocumentoConTypeId> Documenti,
        IReadOnlyList<VoceAnagrafica> TipiAlloggiato,
        string? ComuneStruttura);

    public async Task<RisultatoInvioPayTourist> InviaOraAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceWrite, cancellationToken);
        // automatico: false — un "Invia ora" premuto dall'operatore non deve consumare i tentativi
        // della giornata, altrimenti qualche tentativo a mano andato male zittirebbe il job serale.
        return await InviaSistemaAsync(strutturaId, cancellationToken, automatico: false);
    }

    /// <summary>Usato dal job Quartz schedulato (Worker) — nessun ICurrentUser, gira per conto del sistema.</summary>
    public async Task<RisultatoInvioPayTourist> InviaSistemaAsync(Guid strutturaId, CancellationToken cancellationToken, bool automatico = true)
    {
        await concessioneGuard.EnsurePayTouristAsync(strutturaId, cancellationToken);

        var lista = await payTouristStrutture.ListByStrutturaAsync(strutturaId, cancellationToken);
        if (lista.Count == 0)
        {
            await logEventi.RegistraAsync(
                LivelloLog.Info,
                "Invio PayTourist: 0/0 prenotazioni inviate — nessuna struttura PayTourist configurata.",
                origine: "PayTourist",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "PayTourist",
                cancellationToken: cancellationToken);
            return new RisultatoInvioPayTourist(0, 0, 0, "Nessuna struttura PayTourist configurata.");
        }

        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new PayTouristIntegrazione { StrutturaId = strutturaId };
        if (string.IsNullOrWhiteSpace(integrazione.Token))
        {
            return await SegnalaErroreGlobaleAsync(lista, "Token PayTourist non configurato.", EsitoTentativo.ErroreDefinitivo, automatico, cancellationToken);
        }

        int idSoftware;
        try
        {
            idSoftware = await wubookLicenzaService.GetIdPaytouristAsync(cancellationToken);
        }
        catch (ConflictException ex)
        {
            return await SegnalaErroreGlobaleAsync(lista, ex.Message, EsitoTentativo.ErroreDefinitivo, automatico, cancellationToken);
        }

        var anagraficaDati = await CaricaAnagraficaAsync(strutturaId, cancellationToken);

        int totaleInviate = 0, totalePrenotazioni = 0, totaleErrori = 0;
        var messaggiErrore = new List<string>();

        var adesso = DateTime.UtcNow;

        foreach (var payTouristStruttura in lista)
        {
            // Ogni struttura PayTourist ha i suoi tentativi: una con le credenziali a posto non
            // deve fermarsi perché un'altra della stessa Struttura è mal configurata.
            if (automatico && !PoliticaTentativi.PuoTentare(payTouristStruttura, adesso))
            {
                continue;
            }

            var (inviate, trovate, erroriStruttura, messaggi) = await ProcessaStrutturaAsync(
                strutturaId, payTouristStruttura, integrazione.Token, idSoftware, integrazione.PortaleOnlineAttivo, anagraficaDati, automatico, cancellationToken);

            totaleInviate += inviate;
            totalePrenotazioni += trovate;
            totaleErrori += erroriStruttura;
            messaggiErrore.AddRange(messaggi);
        }

        var messaggio = messaggiErrore.Count == 0 ? null : $"{totaleErrori} prenotazione/i non inviata/e: {string.Join(" | ", messaggiErrore.Distinct())}";
        return new RisultatoInvioPayTourist(totaleInviate, totalePrenotazioni, totaleErrori, messaggio);
    }

    /// <summary>
    /// Esportazione su richiesta (download) delle prenotazioni pronte per una struttura PayTourist —
    /// fallback quando il servizio non è ancora configurato o non è raggiungibile, senza inviarle né
    /// marcarle come inviate (stesso pattern "on-demand, mai persistito" di PDF/XML fattura in Fase 4
    /// e delle schedine Alloggiati Web in Fase 6). Best-effort su riduzioni/portali: se non
    /// disponibili (licenza/token mancanti, servizio irraggiungibile), esporta comunque senza
    /// applicare riduzioni/arricchimento portale, non solleva mai un errore verso l'utente.
    /// </summary>
    public async Task<string> EsportaAsync(ICurrentUser currentUser, Guid strutturaId, Guid payTouristStrutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var payTouristStruttura = await payTouristStrutture.GetAsync(strutturaId, payTouristStrutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura PayTourist non trovata.");

        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new PayTouristIntegrazione { StrutturaId = strutturaId };

        var tipologieIds = payTouristStruttura.Tipologie.Select(t => t.TipologiaId).ToHashSet();
        var daInviare = await ospiti.ListDaInviarePayTouristAsync(strutturaId, tipologieIds, cancellationToken);

        var anagraficaDati = await CaricaAnagraficaAsync(strutturaId, cancellationToken);

        IReadOnlyList<PayTouristRiduzioneDto> riduzioni = [];
        IReadOnlyList<PayTouristPortaleDto> portali = [];
        int idSoftware = 0;

        if (payTouristStruttura.IdStrutturaPaytourist is { } idStruttura && !string.IsNullOrWhiteSpace(integrazione.Token))
        {
            try
            {
                idSoftware = await wubookLicenzaService.GetIdPaytouristAsync(cancellationToken);
                var (riduzioniOk, riduzioniRisultato, _) = await client.GetRiduzioniAsync(integrazione.Token, anagraficaDati.ComuneStruttura, idStruttura, idSoftware, cancellationToken);
                riduzioni = riduzioniOk ? riduzioniRisultato : [];

                if (integrazione.PortaleOnlineAttivo)
                {
                    var (portaliOk, portaliRisultato, _) = await client.GetPortaliOnlineAsync(integrazione.Token, anagraficaDati.ComuneStruttura, idStruttura, idSoftware, cancellationToken);
                    portali = portaliOk ? portaliRisultato : [];
                }
            }
            catch (ConflictException)
            {
                idSoftware = 0;
            }
        }

        var builder = new PayTouristDtoBuilder(anagraficaDati.Luoghi, anagraficaDati.Documenti, anagraficaDati.TipiAlloggiato, riduzioni, portali, anagraficaDati.ComuneStruttura);

        var righe = daInviare.Select(o =>
        {
            var (reservation, motivoScarto) = builder.Costruisci(o, integrazione.PortaleOnlineAttivo);
            return reservation is not null
                ? client.SerializzaPerExport(payTouristStruttura.IdStrutturaPaytourist ?? 0, idSoftware, reservation)
                : $"// SALTATA: {motivoScarto}";
        });

        return string.Join("\r\n", righe);
    }

    /// <summary>
    /// Esportazione di tutte le strutture PayTourist configurate in un unico file: la schermata non
    /// fa più scegliere una struttura, quindi il pulsante esporta l'insieme. Ogni blocco è preceduto
    /// dal nome della struttura, così resta chiaro a quale appartiene ciascuna riga.
    /// </summary>
    public async Task<string> EsportaTutteAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var lista = await payTouristStrutture.ListByStrutturaAsync(strutturaId, cancellationToken);
        var blocchi = new List<string>();

        foreach (var payTouristStruttura in lista)
        {
            var righe = await EsportaAsync(currentUser, strutturaId, payTouristStruttura.Id, cancellationToken);
            blocchi.Add($"// === {payTouristStruttura.Nome} ==={Environment.NewLine}{righe}");
        }

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", blocchi);
    }

    /// <summary>Mappa TipologiaId → struttura PayTourist che la dichiara: l'associazione è univoca, quindi la destinazione non è mai ambigua.</summary>
    private async Task<Dictionary<Guid, PayTouristStruttura>> RisolviStrutturePerTipologiaAsync(Guid strutturaId, CancellationToken cancellationToken)
    {
        var lista = await payTouristStrutture.ListByStrutturaAsync(strutturaId, cancellationToken);

        return lista
            .SelectMany(p => p.Tipologie.Select(t => (t.TipologiaId, Struttura: p)))
            .GroupBy(x => x.TipologiaId)
            .ToDictionary(g => g.Key, g => g.First().Struttura);
    }

    /// <summary>
    /// Invio della singola prenotazione. La struttura PayTourist non va indicata: si deduce dalla
    /// tipologia della camera dell'ospite, che è ciò che stabilisce dove va dichiarata — così una
    /// prenotazione non può finire nella struttura sbagliata per una selezione distratta.
    /// </summary>
    public async Task InviaSingolaAsync(ICurrentUser currentUser, Guid strutturaId, Guid ospiteId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceWrite, cancellationToken);
        await concessioneGuard.EnsurePayTouristAsync(strutturaId, cancellationToken);

        var ospiteRichiesto = await ospiti.GetConPrenotazioneAsync(strutturaId, ospiteId, cancellationToken)
            ?? throw new NotFoundException("Ospite non trovato.");

        var perTipologia = await RisolviStrutturePerTipologiaAsync(strutturaId, cancellationToken);
        var destinazione = ospiteRichiesto.Prenotazione?.Camera?.TipologiaId is { } tipologiaRichiesta && perTipologia.TryGetValue(tipologiaRichiesta, out var trovata)
            ? trovata
            : throw new ConflictException("Nessuna struttura PayTourist associata alla tipologia di questa camera: collegala a una struttura in Impostazioni prima di trasmettere.");

        var payTouristStrutturaId = destinazione.Id;

        string? nomeStruttura = null;
        try
        {
            var payTouristStruttura = await payTouristStrutture.GetAsync(strutturaId, payTouristStrutturaId, cancellationToken)
                ?? throw new NotFoundException("Struttura PayTourist non trovata.");
            nomeStruttura = payTouristStruttura.Nome;

            if (payTouristStruttura.IdStrutturaPaytourist is not { } idStruttura)
            {
                throw new ConflictException("Id struttura PayTourist non configurato.");
            }

            var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
                ?? new PayTouristIntegrazione { StrutturaId = strutturaId };
            if (string.IsNullOrWhiteSpace(integrazione.Token))
            {
                throw new ConflictException("Token PayTourist non configurato.");
            }

            var idSoftware = await wubookLicenzaService.GetIdPaytouristAsync(cancellationToken);

            var tipologieIds = payTouristStruttura.Tipologie.Select(t => t.TipologiaId).ToHashSet();
            var daInviare = await ospiti.ListDaInviarePayTouristAsync(strutturaId, tipologieIds, cancellationToken);
            var ospite = daInviare.FirstOrDefault(o => o.Id == ospiteId);

            if (ospite is null)
            {
                // La selezione esclude già i soggiorni conclusi da più di 7 giorni: senza questo
                // controllo l'operatore leggerebbe "ospite non trovato", vero ma fuorviante — la
                // riga è lì davanti a lui, solo fuori termine.
                var richiesto = await ospiti.GetConPrenotazioneAsync(strutturaId, ospiteId, cancellationToken);
                if (richiesto?.Prenotazione is { } prenotazioneRichiesta && !TerminiPayTourist.IsInTermine(prenotazioneRichiesta.CheckOut, DateTime.UtcNow))
                {
                    throw new ConflictException(
                        $"Termine scaduto: la trasmissione a PayTourist è possibile entro {TerminiPayTourist.GiorniDalCheckOut} giorni dal check-out (era il {prenotazioneRichiesta.CheckOut:dd/MM/yyyy}).");
                }

                throw new NotFoundException("Ospite non trovato tra quelli da inviare per questa struttura PayTourist.");
            }

            var anagraficaDati = await CaricaAnagraficaAsync(strutturaId, cancellationToken);

            var (riduzioniOk, riduzioni, riduzioniErrore) = await client.GetRiduzioniAsync(integrazione.Token, anagraficaDati.ComuneStruttura, idStruttura, idSoftware, cancellationToken);
            if (!riduzioniOk)
            {
                throw new ConflictException(riduzioniErrore ?? "Impossibile recuperare le riduzioni PayTourist.");
            }

            IReadOnlyList<PayTouristPortaleDto> portali = [];
            if (integrazione.PortaleOnlineAttivo)
            {
                var (portaliOk, portaliRisultato, portaliErrore) = await client.GetPortaliOnlineAsync(integrazione.Token, anagraficaDati.ComuneStruttura, idStruttura, idSoftware, cancellationToken);
                if (!portaliOk)
                {
                    throw new ConflictException(portaliErrore ?? "Impossibile recuperare i portali online PayTourist.");
                }

                portali = portaliRisultato;
            }

            var builder = new PayTouristDtoBuilder(anagraficaDati.Luoghi, anagraficaDati.Documenti, anagraficaDati.TipiAlloggiato, riduzioni, portali, anagraficaDati.ComuneStruttura);

            var (reservation, motivoScarto) = builder.Costruisci(ospite, integrazione.PortaleOnlineAttivo);
            if (reservation is null)
            {
                throw new ConflictException(motivoScarto ?? "Impossibile costruire la prenotazione per l'invio.");
            }

            var esito = await client.InviaPrenotazioneAsync(integrazione.Token, anagraficaDati.ComuneStruttura, idStruttura, idSoftware, reservation, cancellationToken);
            if (!esito.Ok)
            {
                throw new ConflictException(esito.Errore ?? "Invio rifiutato da PayTourist.");
            }

            await MarcaInviataAsync(ospite.PrenotazioneId, cancellationToken);

            await LogInvioSingoloAsync(
                strutturaId,
                LivelloLog.Info,
                $"Invio singolo PayTourist ({payTouristStruttura.Nome}): {ospite.Nome} {ospite.Cognome} inviato.",
                currentUser.Email,
                cancellationToken);
        }
        catch (ConflictException ex)
        {
            await LogInvioSingoloAsync(
                strutturaId,
                LivelloLog.Warning,
                nomeStruttura is null ? $"Invio singolo PayTourist: {ex.Message}" : $"Invio singolo PayTourist ({nomeStruttura}): {ex.Message}",
                currentUser.Email,
                cancellationToken);
            throw;
        }
    }

    /// <summary>Invii/verifiche fatti a mano dall'operatore (a differenza di SalvaEsitoAsync, che copre solo il job automatico) devono comparire nel Log tanto quanto quelli automatici — gap reale: prima di questa aggiunta un invio singolo fallito non lasciava traccia da nessuna parte, scoperto testando con l'account PayTourist di prova.</summary>
    private async Task LogInvioSingoloAsync(Guid strutturaId, LivelloLog livello, string messaggio, string? operatore, CancellationToken cancellationToken) =>
        await logEventi.RegistraAsync(
            livello,
            messaggio,
            origine: "PayTourist",
            clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
            strutturaId: strutturaId,
            categoria: "PayTourist",
            operatore: operatore,
            cancellationToken: cancellationToken);

    /// <summary>Elenco prenotazioni dell'anno indicato (sul check-out) di una struttura PayTourist per la schermata operativa — da inviare e già inviate.</summary>
    /// <summary>
    /// Elenco delle prenotazioni concluse dell'anno per l'intera Struttura, non per singola struttura
    /// PayTourist: ogni riga porta con sé quella in cui va dichiarata, dedotta dalla tipologia della
    /// camera (associazione univoca). L'operatore le vede tutte in un elenco solo e le riconosce
    /// dalla colonna Camera.
    /// </summary>
    public async Task<IReadOnlyList<PrenotazionePayTourist>> ListPrenotazioniAsync(ICurrentUser currentUser, Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);

        var perTipologia = await RisolviStrutturePerTipologiaAsync(strutturaId, cancellationToken);
        var recenti = await ospiti.ListRecentiPayTouristAsync(strutturaId, tipologieIds: null, anno, cancellationToken);

        var adesso = DateTime.UtcNow;

        return recenti
            // Senza struttura PayTourist associata non c'è dove dichiararla: la riga non compare
            // affatto (scelta esplicita dell'utente — le tipologie non assegnate sono tali apposta).
            .Where(o => o.Prenotazione?.Camera?.TipologiaId is { } t && perTipologia.ContainsKey(t))
            .Select(o =>
        {
            var destinazione = o.Prenotazione?.Camera?.TipologiaId is { } tipologiaId && perTipologia.TryGetValue(tipologiaId, out var trovata) ? trovata : null;

            return new PrenotazionePayTourist(
                o.Id,
                o.PrenotazioneId,
                $"{o.Cognome} {o.Nome}".Trim(),
                o.Prenotazione?.Camera?.Nome,
                o.Prenotazione?.CheckIn,
                o.Prenotazione?.CheckOut,
                o.Prenotazione?.PayTourist ?? false,
                TerminiPayTourist.ScadenzaUtc(o.Prenotazione?.CheckOut),
                // Senza struttura PayTourist associata non c'è dove dichiararla: non trasmissibile
                // finché la tipologia non viene collegata in Impostazioni.
                destinazione is not null && TerminiPayTourist.IsInTermine(o.Prenotazione?.CheckOut, adesso),
                destinazione?.Id,
                destinazione?.Nome);
        }).ToList();
    }

    /// <summary>Anni con almeno una prenotazione per il selettore Anno della schermata operativa — su richiesta esplicita, non deve proporre anni sicuramente vuoti.</summary>
    public async Task<IReadOnlyList<int>> ListaAnniAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceRead, cancellationToken);
        return await prenotazioni.ListaAnniConPrenotazioniAsync(strutturaId, cancellationToken);
    }

    private async Task<(int Inviate, int Trovate, int Errori, IReadOnlyList<string> Messaggi)> ProcessaStrutturaAsync(
        Guid strutturaId,
        PayTouristStruttura payTouristStruttura,
        string token,
        int idSoftware,
        bool portaleOnlineAttivo,
        AnagraficaPayTourist anagraficaDati,
        bool automatico,
        CancellationToken cancellationToken)
    {
        // null quando l'invio è partito a mano: i tentativi contano solo per il job serale.
        EsitoTentativo? Tentativo(EsitoTentativo esito) => automatico ? esito : null;
        if (payTouristStruttura.IdStrutturaPaytourist is not { } idStruttura)
        {
            await SalvaEsitoAsync(payTouristStruttura, 0, 0, "Id struttura PayTourist non configurato.", Tentativo(EsitoTentativo.ErroreDefinitivo), cancellationToken);
            return (0, 0, 0, []);
        }

        if (payTouristStruttura.Tipologie.Count == 0)
        {
            await SalvaEsitoAsync(payTouristStruttura, 0, 0, "Nessuna tipologia camera associata a questa struttura PayTourist.", Tentativo(EsitoTentativo.ErroreDefinitivo), cancellationToken);
            return (0, 0, 0, []);
        }

        var tipologieIds = payTouristStruttura.Tipologie.Select(t => t.TipologiaId).ToHashSet();
        var daInviare = await ospiti.ListDaInviarePayTouristAsync(strutturaId, tipologieIds, cancellationToken);
        if (daInviare.Count == 0)
        {
            await SalvaEsitoAsync(payTouristStruttura, 0, 0, null, Tentativo(EsitoTentativo.Riuscito), cancellationToken);
            return (0, 0, 0, []);
        }

        daInviare = await EscludiGiaDichiarateAsync(strutturaId, daInviare, token, anagraficaDati.ComuneStruttura, idStruttura, idSoftware, cancellationToken);
        if (daInviare.Count == 0)
        {
            await SalvaEsitoAsync(payTouristStruttura, 0, 0, null, Tentativo(EsitoTentativo.Riuscito), cancellationToken);
            return (0, 0, 0, []);
        }

        var (riduzioniOk, riduzioni, riduzioniErrore) = await client.GetRiduzioniAsync(token, anagraficaDati.ComuneStruttura, idStruttura, idSoftware, cancellationToken);
        if (!riduzioniOk)
        {
            var errore = riduzioniErrore ?? "Impossibile recuperare le riduzioni PayTourist.";
            await SalvaEsitoAsync(payTouristStruttura, 0, daInviare.Count, errore, Tentativo(EsitoTentativo.ErroreRitentabile), cancellationToken);
            return (0, daInviare.Count, daInviare.Count, [errore]);
        }

        IReadOnlyList<PayTouristPortaleDto> portali = [];
        if (portaleOnlineAttivo)
        {
            var (portaliOk, portaliRisultato, portaliErrore) = await client.GetPortaliOnlineAsync(token, anagraficaDati.ComuneStruttura, idStruttura, idSoftware, cancellationToken);
            if (!portaliOk)
            {
                var errore = portaliErrore ?? "Impossibile recuperare i portali online PayTourist.";
                await SalvaEsitoAsync(payTouristStruttura, 0, daInviare.Count, errore, Tentativo(EsitoTentativo.ErroreRitentabile), cancellationToken);
                return (0, daInviare.Count, daInviare.Count, [errore]);
            }

            portali = portaliRisultato;
        }

        var builder = new PayTouristDtoBuilder(anagraficaDati.Luoghi, anagraficaDati.Documenti, anagraficaDati.TipiAlloggiato, riduzioni, portali, anagraficaDati.ComuneStruttura);

        var inviate = 0;
        string? ultimoErrore = null;
        var messaggi = new List<string>();

        foreach (var ospite in daInviare)
        {
            var (reservation, motivoScarto) = builder.Costruisci(ospite, portaleOnlineAttivo);
            if (reservation is null)
            {
                ultimoErrore = motivoScarto;
                messaggi.Add(motivoScarto!);
                continue;
            }

            try
            {
                var esito = await client.InviaPrenotazioneAsync(token, anagraficaDati.ComuneStruttura, idStruttura, idSoftware, reservation, cancellationToken);
                if (!esito.Ok)
                {
                    ultimoErrore = esito.Errore ?? "Invio rifiutato.";
                    messaggi.Add(ultimoErrore);
                    continue;
                }

                await MarcaInviataAsync(ospite.PrenotazioneId, cancellationToken);
                inviate++;
            }
            catch (Exception ex)
            {
                ultimoErrore = ex.Message;
                messaggi.Add(ex.Message);
            }
        }

        // Come per Alloggiati Web: se almeno una è passata il servizio risponde, e gli scarti sono
        // dei singoli dati — ritentare l'intero lotto non li correggerebbe.
        var esitoLotto = ultimoErrore is null
            ? EsitoTentativo.Riuscito
            : inviate > 0 ? EsitoTentativo.ErroreDefinitivo : EsitoTentativo.ErroreRitentabile;
        await SalvaEsitoAsync(payTouristStruttura, inviate, daInviare.Count, ultimoErrore, Tentativo(esitoLotto), cancellationToken);
        return (inviate, daInviare.Count, daInviare.Count - inviate, messaggi);
    }

    private async Task<AnagraficaPayTourist> CaricaAnagraficaAsync(Guid strutturaId, CancellationToken cancellationToken) => new(
        await anagrafica.ListLuoghiAsync(cancellationToken),
        await anagrafica.ListDocumentiConTypeIdAsync(cancellationToken),
        await anagrafica.ListTipiAlloggiatoAsync(cancellationToken),
        (await impostazioniStruttura.GetByStrutturaIdAsync(strutturaId, cancellationToken))?.ComuneAttivita);

    private async Task<RisultatoInvioPayTourist> SegnalaErroreGlobaleAsync(
        IReadOnlyList<PayTouristStruttura> lista,
        string errore,
        EsitoTentativo esito,
        bool automatico,
        CancellationToken cancellationToken)
    {
        foreach (var payTouristStruttura in lista)
        {
            await SalvaEsitoAsync(payTouristStruttura, 0, 0, errore, automatico ? esito : null, cancellationToken);
        }

        return new RisultatoInvioPayTourist(0, 0, 0, errore);
    }

    /// <summary>
    /// Toglie dal lotto le prenotazioni che PayTourist mostra già come dichiarate e le marca come
    /// inviate, così non si ripresentano al giro successivo.
    ///
    /// Esiste perché su PayTourist un doppione non è un fastidio come una schedina alloggiati
    /// inviata due volte: è imposta di soggiorno chiesta due volte allo stesso ospite, e l'Api non
    /// offre nessun modo per annullarla. E i modi in cui può nascere non sono solo i nostri
    /// reinvii: una risposta persa dopo che il portale aveva già registrato, oppure il file di
    /// Pubblica Sicurezza caricato a mano sul portale dall'albergatore (PayTourist lo importa),
    /// producono lo stesso risultato senza che il gestionale ne sappia nulla.
    ///
    /// Il confronto è esatto perché la chiave è deterministica da entrambe le parti, vedi
    /// <see cref="PayTouristDtoBuilder.PartnerIdPrenotazione"/>.
    /// </summary>
    private async Task<IReadOnlyList<Ospite>> EscludiGiaDichiarateAsync(
        Guid strutturaId,
        IReadOnlyList<Ospite> daInviare,
        string token,
        string? comuneAttivita,
        int idStruttura,
        int idSoftware,
        CancellationToken cancellationToken)
    {
        var conPrenotazione = daInviare.Where(o => o.Prenotazione is not null).ToList();
        if (conPrenotazione.Count == 0)
        {
            return daInviare;
        }

        var dateCheckIn = conPrenotazione.Select(o => o.Prenotazione!.CheckIn ?? DateTime.UtcNow.Date).ToList();

        var (ok, dichiarazioni, errore) = await client.GetDichiarazioniEsistentiAsync(
            token, comuneAttivita, idStruttura, idSoftware, dateCheckIn, cancellationToken);

        if (!ok)
        {
            // L'invio non si blocca: il controllo è una protezione in più, non una condizione per
            // dichiarare — e se il portale non risponde qui, molto probabilmente non risponderà
            // nemmeno all'invio. Resta però scritto che questa volta la verifica non c'è stata: è
            // l'unica traccia utile se in seguito salta fuori un doppione.
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                $"Controllo anti-duplicato PayTourist non eseguito: {errore ?? "elenco prenotazioni non disponibile."} L'invio prosegue senza verifica.",
                origine: "PayTourist",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "PayTourist",
                cancellationToken: cancellationToken);

            return daInviare;
        }

        if (!dichiarazioni.Completo)
        {
            // Elenco troncato: quello che c'è dentro vale (una prenotazione trovata è davvero già
            // dichiarata), ma le mancanti non sono necessariamente da dichiarare. Non si blocca
            // niente, si scrive che il controllo di oggi ha visto solo una parte.
            await logEventi.RegistraAsync(
                LivelloLog.Warning,
                "Controllo anti-duplicato PayTourist parziale: l'elenco delle prenotazioni del portale è stato troncato. Una prenotazione già dichiarata potrebbe non essere stata riconosciuta.",
                origine: "PayTourist",
                clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
                strutturaId: strutturaId,
                categoria: "PayTourist",
                cancellationToken: cancellationToken);
        }

        var partnerIdDichiarati = dichiarazioni.PartnerIdPrenotazioni.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var anagraficheDichiarate = dichiarazioni.Ospiti
            .Select(o => PayTouristDtoBuilder.ChiaveOspite(o.NomeCompleto, o.DataNascita, o.CheckIn))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var giaPresenti = conPrenotazione
            .Where(o => GiaDichiarata(o, partnerIdDichiarati, anagraficheDichiarate))
            .ToList();

        if (giaPresenti.Count == 0)
        {
            return daInviare;
        }

        foreach (var ospite in giaPresenti)
        {
            await MarcaInviataAsync(ospite.PrenotazioneId, cancellationToken);
        }

        await logEventi.RegistraAsync(
            LivelloLog.Info,
            $"PayTourist: {giaPresenti.Count} prenotazione/i risultavano già dichiarate sul portale, marcate come inviate senza ritrasmetterle.",
            origine: "PayTourist",
            clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
            strutturaId: strutturaId,
            categoria: "PayTourist",
            cancellationToken: cancellationToken);

        var esclusi = giaPresenti.Select(o => o.Id).ToHashSet();
        return daInviare.Where(o => !esclusi.Contains(o.Id)).ToList();
    }

    /// <summary>
    /// Due modi di riconoscere una prenotazione già dichiarata, perché due sono i modi in cui può
    /// essere finita sul portale:
    /// <list type="number">
    /// <item>l'abbiamo dichiarata noi (anche solo perché una risposta si è persa dopo che il portale
    /// aveva già registrato): la si riconosce dalla nostra chiave;</item>
    /// <item>ce l'ha messa una persona, caricando a mano il file di Pubblica Sicurezza, che PayTourist
    /// importa: lì la nostra chiave non esiste e l'unico appiglio è l'anagrafica del capofamiglia —
    /// nome, cognome, data di nascita e giorno di arrivo. È il capofamiglia a bastare: se lui
    /// risulta già dichiarato, quel soggiorno è stato caricato per intero, non a metà.</item>
    /// </list>
    /// Senza data di nascita il confronto anagrafico non si fa affatto (vedi
    /// <see cref="PayTouristDtoBuilder.ChiaveOspite"/>): meglio rischiare un doppione che saltare
    /// una dichiarazione scambiando due omonimi per la stessa persona.
    /// </summary>
    private static bool GiaDichiarata(Ospite ospite, IReadOnlySet<string> partnerIdDichiarati, IReadOnlySet<string> anagraficheDichiarate)
    {
        if (partnerIdDichiarati.Contains(PayTouristDtoBuilder.PartnerIdPrenotazione(ospite.Prenotazione!)))
        {
            return true;
        }

        var checkIn = ospite.Prenotazione!.CheckIn;

        var chiavi = new List<string?> { PayTouristDtoBuilder.ChiaveOspite(ospite.Nome, ospite.Cognome, ospite.DataNascita, checkIn) };
        chiavi.AddRange(ospite.Membri.Select(m => PayTouristDtoBuilder.ChiaveOspite(m.Nome, m.Cognome, m.DataNascita, checkIn)));

        // Devono esserci **tutti**, non solo l'intestatario. Se anche uno solo dei suoi ospiti non
        // risulta dichiarato, questa prenotazione va trasmessa: fermarsi all'intestatario basterebbe
        // a saltare l'intera prenotazione — membri compresi — nel caso in cui la stessa persona
        // compaia come capofamiglia di due soggiorni con lo stesso arrivo. Meglio rischiare che una
        // persona venga dichiarata due volte, che lasciarne indietro tre.
        // Una chiave nulla (manca la data di nascita) non è verificabile: conta come "non trovato".
        return chiavi.All(chiave => chiave is not null && anagraficheDichiarate.Contains(chiave));
    }

    private async Task MarcaInviataAsync(Guid? prenotazioneId, CancellationToken cancellationToken)
    {
        if (prenotazioneId is not { } id)
        {
            return;
        }

        var prenotazione = await prenotazioni.GetAsync(id, cancellationToken);
        if (prenotazione is null)
        {
            return;
        }

        prenotazione.PayTourist = true;
        prenotazione.UpdatedAtUtc = DateTime.UtcNow;
        await prenotazioni.UpdateAsync(prenotazione, cancellationToken);
    }

    private async Task SalvaEsitoAsync(
        PayTouristStruttura entity,
        int inviate,
        int totale,
        string? errore,
        // null = invio partito a mano (Invia ora / invio singolo): non consuma i tentativi del job
        // e va sempre a log, perché è la risposta a un'azione appena compiuta dall'operatore.
        EsitoTentativo? esitoTentativo,
        CancellationToken cancellationToken)
    {
        var adesso = DateTime.UtcNow;
        var tentativiEsauriti = esitoTentativo is { } esito && PoliticaTentativi.RegistraEsito(entity, esito, adesso);

        entity.UltimoInvioAtUtc = adesso;
        entity.UltimeInviate = inviate;
        entity.UltimoErrore = errore;
        await payTouristStrutture.UpdateAsync(entity, cancellationToken);

        // Un log all'invio riuscito, anche "0/0 prenotazioni" (nessuna da inviare oggi): serve a
        // vedere dalla pagina Log che il job gira per questa struttura. I fallimenti si annotano
        // invece solo all'ultimo tentativo utile, con il conto di quelli spesi.
        if (esitoTentativo is { } e && e != EsitoTentativo.Riuscito && !tentativiEsauriti)
        {
            return;
        }

        var resa = esitoTentativo is null
            ? string.Empty
            : $" ({(entity.TentativiFallitiOggi == 1 ? "1 tentativo" : $"{entity.TentativiFallitiOggi} tentativi falliti")}; nuovo tentativo domani all'orario configurato)";

        if (tentativiEsauriti && errore is not null)
        {
            await notificaService.CreaSeNonEsisteAsync(
                entity.StrutturaId,
                TipoNotifica.InvioSchedineNonRiuscito,
                $"invio-non-riuscito:PayTourist:{entity.Nome}:{adesso:yyyyMMdd}",
                "PayTourist: invio non riuscito",
                $"{entity.Nome}: {errore} Nuovo tentativo domani all'orario configurato.",
                cancellationToken);
        }
        var messaggio = errore is null
            ? $"Invio PayTourist ({entity.Nome}): {inviate}/{totale} prenotazioni inviate."
            : $"Invio PayTourist ({entity.Nome}): {inviate}/{totale} prenotazioni inviate — {errore}{resa}.";

        await logEventi.RegistraAsync(
            errore is null ? LivelloLog.Info : LivelloLog.Warning,
            messaggio,
            origine: "PayTourist",
            clienteId: await strutture.GetClienteIdAsync(entity.StrutturaId, cancellationToken),
            strutturaId: entity.StrutturaId,
            categoria: "PayTourist",
            cancellationToken: cancellationToken);
    }
}
