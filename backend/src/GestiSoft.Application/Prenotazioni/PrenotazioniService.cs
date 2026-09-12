using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Notifiche;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Prenotazioni;

public record CreaPrenotazioneRequest(
    // Esattamente uno tra i due va indicato: CameraId per scegliere una camera specifica (come
    // sempre), TipologiaId per prenotare "una qualsiasi camera di questo tipo" e lasciare che il
    // sistema assegni la prima libera (v. AssegnazioneCameraService) — pool di camere identiche.
    Guid? CameraId,
    Guid? TipologiaId,
    string? Agenzia,
    string? NumeroPrenotazione,
    decimal? ImportoPrenotazione,
    decimal? ImportoPagato,
    decimal? ImportoTotale,
    DateTime CheckIn,
    DateTime CheckOut,
    int? NumeroOspiti,
    bool TassaSoggiornoAttiva = true,
    bool SpesePuliziaAttiva = true,
    bool AnimaliAttiva = false,
    bool CauzioneAttiva = true);

public record AggiornaPrenotazioneRequest(
    Guid? CameraId,
    Guid? TipologiaId,
    string? Agenzia,
    string? NumeroPrenotazione,
    decimal? ImportoPrenotazione,
    decimal? ImportoPagato,
    decimal? ImportoTotale,
    DateTime CheckIn,
    DateTime CheckOut,
    int? NumeroOspiti,
    bool TassaSoggiornoAttiva = true,
    bool SpesePuliziaAttiva = true,
    bool AnimaliAttiva = false,
    bool CauzioneAttiva = true,
    // Orario reale dell'arrivo, correggibile quando il check-in è stato registrato in ritardo: da
    // qui decorrono i termini della schedina alloggiati (vedi TerminiSchedina). Null = lascia
    // quello già registrato, così un salvataggio qualsiasi non lo azzera.
    DateTime? CheckInEffettuatoAtUtc = null);

public record CheckOutRequest(bool RestituisciCauzione, decimal? ImportoCauzioneTrattenuta);

/// <summary>
/// Prenotazioni (soggiorni) e stato delle camere — porta OspitiLogic.AddOrUpdateOspiti (solo la
/// parte testata Prenotazione, la scheda Ospiti è in OspitiService) e RoomSettingLogic.ChangeRoomStatus
/// del legacy (vedi report Fase 3 sez. C.2/C.3). A differenza del legacy, verifica la
/// sovrapposizione di prenotazioni sulla stessa camera prima di salvare (decisione presa con
/// l'utente: nel legacy questo controllo mancava, qui il sistema è multi-utente via HTTP).
/// </summary>
public class PrenotazioniService(
    IPrenotazioneRepository prenotazioni,
    ICameraRepository camere,
    ICauzioneRepository cauzioni,
    IOspiteRepository ospiti,
    IStrutturaRepository strutture,
    PermessoStrutturaGuard permessoGuard,
    ILogEventoService logEventi,
    OspitiService ospitiService,
    NotificaService notificaService,
    AssegnazioneCameraService assegnazioneCamera,
    WubookDisponibilitaService disponibilitaOta,
    WubookLicenzaService licenzaOta)
{
    /// <summary>
    /// Spinge subito la disponibilità aggiornata su Wubook per il periodo appena toccato (creazione,
    /// cambio camera/date, annullamento) — mai in differita: la camera che qui si libera o si occupa
    /// deve risultare libera o occupata anche lato OTA nello stesso momento, non al prossimo giro di
    /// un job né quando l'operatore si ricorda di premere "Sincronizza disponibilità" a mano — un
    /// ritardo qui è esattamente la finestra in cui può capitare un overbooking.
    /// La prenotazione locale resta comunque la fonte di verità: se la struttura non ha un'
    /// integrazione OTA (il caso più comune, salta subito senza nemmeno tentare la chiamata) il
    /// salvataggio non viene mai bloccato da questo. Se invece l'OTA risultava configurato e la
    /// chiamata fallisce (Wubook irraggiungibile o l'ha rifiutata), quello è un vero rischio di
    /// overbooking: va segnalato subito nel pannello "Stato sincronizzazione" di Servizi OTA invece
    /// di sparire in silenzio, anche se non blocca comunque il salvataggio già avvenuto in locale.
    /// </summary>
    private async Task SincronizzaDisponibilitaOtaAsync(Guid strutturaId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken)
    {
        if (!await disponibilitaOta.HaSincronizzazioneAttivaAsync(strutturaId, cancellationToken))
        {
            return;
        }

        try
        {
            await disponibilitaOta.SincronizzaSistemaAsync(strutturaId, dataInizio.Date, dataFine.Date, cancellationToken);
            await licenzaOta.SegnalaEsitoSincronizzazioneAsync(strutturaId, null, cancellationToken);
        }
        catch (Exception ex)
        {
            var messaggio = ex is DomainException dominio
                ? dominio.Message
                : "Sincronizzazione automatica della disponibilità con l'OTA non riuscita: usa \"Sincronizza disponibilità\" per riprovare.";
            await licenzaOta.SegnalaEsitoSincronizzazioneAsync(strutturaId, messaggio, cancellationToken);
        }
    }
    /// <summary>
    /// Se il numero non è stato scritto a mano e l'agenzia è "Diretta", genera il progressivo
    /// annuale (conteggio prenotazioni dirette dell'anno + 1) — sia alla creazione sia quando una
    /// prenotazione esistente viene modificata per diventare Diretta con il numero lasciato vuoto.
    /// </summary>
    private async Task<string?> NumeroPrenotazioneOAutoIncrementoAsync(Guid strutturaId, string? agenzia, string? numeroPrenotazione, int anno, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(numeroPrenotazione) || !string.Equals(agenzia, "Diretta", StringComparison.OrdinalIgnoreCase))
        {
            return numeroPrenotazione;
        }

        var conteggio = await prenotazioni.ContaDireteAnnoAsync(strutturaId, anno, cancellationToken);
        return (conteggio + 1).ToString();
    }

    private Task LogPrenotazioneAsync(ICurrentUser currentUser, Guid strutturaId, string messaggio, CancellationToken cancellationToken) =>
        logEventi.RegistraAsync(
            LivelloLog.Info,
            messaggio,
            origine: "Api",
            clienteId: currentUser.ClienteId,
            strutturaId: strutturaId,
            categoria: "Prenotazione",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<Prenotazione>> ListaInArrivoAsync(ICurrentUser currentUser, Guid strutturaId, DateTime? daData, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        return await prenotazioni.ListInArrivoAsync(strutturaId, (daData ?? DateTime.UtcNow).Date, cancellationToken);
    }

    public async Task<IReadOnlyList<Prenotazione>> ListaInCorsoAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        return await prenotazioni.ListInCorsoAsync(strutturaId, cancellationToken);
    }

    /// <summary>
    /// Controllo live di sovrapposizione, richiamato dal form appena si seleziona camera+date (prima
    /// del salvataggio) — stessa regola di <see cref="EnsureNessunaSovrapposizioneAsync"/>, ma qui
    /// ritorna la prenotazione in conflitto (se c'è) invece di lanciare, per poterne mostrare il
    /// dettaglio in UI. Il controllo autorevole resta comunque quello al salvataggio: tra la verifica
    /// live e il click su "Crea" un'altra prenotazione potrebbe nel frattempo occupare la camera.
    /// </summary>
    public async Task<Prenotazione?> VerificaDisponibilitaAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        Guid cameraId,
        DateTime checkIn,
        DateTime checkOut,
        Guid? escludiPrenotazioneId,
        CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        var occupazioni = await prenotazioni.ListOccupazioneAsync(strutturaId, cameraId, checkIn, checkOut, cancellationToken);
        return occupazioni.FirstOrDefault(p => p.Id != (escludiPrenotazioneId ?? Guid.Empty));
    }

    public async Task<IReadOnlyList<Prenotazione>> ListaStoricoAsync(ICurrentUser currentUser, Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        return await prenotazioni.ListStoricoAsync(strutturaId, anno, cancellationToken);
    }

    public async Task<IReadOnlyList<Prenotazione>> ListaPeriodoAsync(ICurrentUser currentUser, Guid strutturaId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        return await prenotazioni.ListPeriodoAsync(strutturaId, dataInizio, dataFine, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListaAgenzieAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        return await prenotazioni.ListaAgenzieAsync(strutturaId, cancellationToken);
    }

    public async Task<Prenotazione> GetAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        return await GetOwnedAsync(strutturaId, prenotazioneId, cancellationToken);
    }

    public async Task<Prenotazione> CreaAsync(ICurrentUser currentUser, Guid strutturaId, CreaPrenotazioneRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);

        if (request.CheckOut.Date <= request.CheckIn.Date)
        {
            throw new ConflictException("La data di check-out deve essere successiva al check-in.");
        }

        // Scelta diretta di una camera (come sempre) oppure solo la Tipologia: in quel caso qui è
        // il percorso "manuale" (l'operatore sta creando la prenotazione lui stesso), quindi se il
        // pool è già pieno per queste date si blocca subito con un errore — a differenza del pull
        // da OTA (WubookPrenotazioniService), che in questo caso registra comunque la prenotazione
        // senza camera invece di perderla, perché lì l'ospite ha già prenotato per davvero altrove.
        var cameraId = request.CameraId
            ?? (request.TipologiaId is { } tipologiaId
                ? (await assegnazioneCamera.RisolviCameraLiberaAsync(strutturaId, tipologiaId, request.CheckIn, request.CheckOut, escludiPrenotazioneId: null, cancellationToken)).Id
                : throw new ConflictException("Specifica una camera o una tipologia."));

        await ValidaCameraECheckInOutAsync(strutturaId, cameraId, request.CheckIn, request.CheckOut, cancellationToken);
        await EnsureNessunaSovrapposizioneAsync(strutturaId, cameraId, request.CheckIn, request.CheckOut, escludiPrenotazioneId: null, cancellationToken);

        var numeroPrenotazione = await NumeroPrenotazioneOAutoIncrementoAsync(strutturaId, request.Agenzia, request.NumeroPrenotazione, request.CheckIn.Year, cancellationToken);

        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        // Fedele a AddOrUpdateOspitiViewModel.Save() del legacy: se la Tassa di soggiorno è
        // disattivata per questa prenotazione, le schedine Alloggiati Web/Osservatorio/PayTourist
        // vengono marcate subito come "già inviate" senza inviare nulla (mai più segnalate come
        // "da inviare"). In più (richiesta esplicita, non presente nel legacy): lo stesso vale,
        // indipendentemente dal toggle, per ciascun servizio che la Struttura non ha proprio
        // ("l'account") — inutile marcare "da inviare" una schedina per un servizio non concesso.
        var tassaDisattivata = !request.TassaSoggiornoAttiva;

        var entity = new Prenotazione
        {
            StrutturaId = strutturaId,
            CameraId = cameraId,
            TipologiaId = request.TipologiaId,
            Agenzia = request.Agenzia,
            NumeroPrenotazione = numeroPrenotazione,
            ImportoPrenotazione = request.ImportoPrenotazione,
            ImportoPagato = request.ImportoPagato,
            ImportoTotale = request.ImportoTotale,
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            NumeroOspiti = request.NumeroOspiti,
            Anno = request.CheckIn.Year,
            // Una prenotazione appena creata è sempre Incompleta finché non viene collegata una
            // scheda ospiti (vedi OspitiService), fedele a OspitiLogic.AddOrUpdateOspiti del legacy.
            StatoPrenotazione = StatoPrenotazione.Incompleta,
            TassaSoggiornoAttiva = request.TassaSoggiornoAttiva,
            SpesePuliziaAttiva = request.SpesePuliziaAttiva,
            AnimaliAttiva = request.AnimaliAttiva,
            CauzioneAttiva = request.CauzioneAttiva,
            StatePolice = tassaDisattivata || !struttura.AlloggiatiWebAbilitato,
            PMS = tassaDisattivata || !struttura.OsservatorioAbilitato,
            PayTourist = tassaDisattivata || !struttura.PayTouristAbilitato,
        };

        await prenotazioni.AddAsync(entity, cancellationToken);
        await SincronizzaDisponibilitaOtaAsync(strutturaId, request.CheckIn, request.CheckOut, cancellationToken);
        await LogPrenotazioneAsync(currentUser, strutturaId, $"Prenotazione creata (camera {cameraId}, {request.CheckIn:dd/MM/yyyy}–{request.CheckOut:dd/MM/yyyy}).", cancellationToken);
        return entity;
    }

    public async Task<Prenotazione> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, AggiornaPrenotazioneRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);

        var entity = await GetOwnedAsync(strutturaId, prenotazioneId, cancellationToken);

        // Un soggiorno Completato è chiuso: l'unica cosa che ha ancora senso correggere sono gli
        // importi (es. un saldo incassato dopo il check-out). Camera/date/toggle restano quelli con
        // cui il soggiorno si è effettivamente svolto — imposti a livello di servizio, non solo di
        // UI, per non fidarsi ciecamente di un client che aggirasse i campi disabilitati.
        var completata = entity.StatoPrenotazione == StatoPrenotazione.Completata;

        // L'orario reale dell'arrivo si corregge anche su un soggiorno già concluso (a differenza di
        // camera/date/toggle): serve a rimettere a posto i termini della schedina alloggiati quando
        // il check-in è stato registrato in ritardo rispetto all'arrivo vero, e ha senso solo su una
        // prenotazione in cui l'ospite è effettivamente arrivato.
        if (request.CheckInEffettuatoAtUtc is { } arrivoCorretto && entity.StatoPrenotazione is StatoPrenotazione.InCorso or StatoPrenotazione.Completata)
        {
            entity.CheckInEffettuatoAtUtc = DateTime.SpecifyKind(arrivoCorretto, DateTimeKind.Utc);
        }

        // Snapshot "prima" dei soli campi con un impatto economico diretto — servono a registrare
        // nel log un vecchio→nuovo esplicito (non solo "modificata"), perché un operatore potrebbe
        // togliere una spunta o abbassare l'importo e incassare la differenza in nero: senza il
        // dettaglio, il log non lo renderebbe rintracciabile.
        var tassaSoggiornoPrima = entity.TassaSoggiornoAttiva;
        var spesePuliziaPrima = entity.SpesePuliziaAttiva;
        var animaliPrima = entity.AnimaliAttiva;
        var cauzionePrima = entity.CauzioneAttiva;
        var importoPrenotazionePrima = entity.ImportoPrenotazione;
        var importoTotalePrima = entity.ImportoTotale;
        var importoPagatoPrima = entity.ImportoPagato;
        var totalTaxPrima = entity.TotalTax;
        // Snapshot delle date "prima" — servono a spingere su Wubook l'intero periodo toccato dalla
        // modifica (sia dove la vecchia camera si libera sia dove la nuova/stessa camera si occupa),
        // non solo le nuove date: se il soggiorno si accorcia o si sposta, i giorni usciti dal nuovo
        // intervallo vanno comunque risincronizzati per tornare "liberi" lato OTA.
        var checkInPrima = entity.CheckIn;
        var checkOutPrima = entity.CheckOut;

        if (!completata)
        {
            if (request.CheckOut.Date <= request.CheckIn.Date)
            {
                throw new ConflictException("La data di check-out deve essere successiva al check-in.");
            }

            // Se la camera scelta in precedenza resta compatibile con le nuove date non la
            // ritocchiamo: rifare la ricerca "prima libera" ad ogni modifica sposterebbe senza motivo
            // un ospite già assegnato a una camera del pool. "Compatibile" richiede però anche che
            // appartenga ancora alla Tipologia appena scelta — altrimenti, cambiando Tipologia in
            // modalità pool, si rischia di tenere la camera della VECCHIA Tipologia (libera per quelle
            // date, ma del pool sbagliato) invece di assegnarne una vera della nuova: bug reale
            // riprodotto dal vivo (cambio da una Tipologia con 151 camere a una con 26, il sistema
            // teneva la camera "100" della prima anche se nella seconda non esisteva affatto).
            Guid cameraId;
            if (request.CameraId is { } cameraIdEsplicita)
            {
                cameraId = cameraIdEsplicita;
            }
            else if (request.TipologiaId is { } tipologiaId)
            {
                var cameraAttuale = entity.CameraId is { } cameraIdAttuale ? await camere.GetAsync(cameraIdAttuale, cancellationToken) : null;
                var restaValida = cameraAttuale is not null
                    && cameraAttuale.TipologiaId == tipologiaId
                    && !await prenotazioni.EsisteSovrapposizioneAsync(strutturaId, cameraAttuale.Id, request.CheckIn, request.CheckOut, prenotazioneId, cancellationToken);
                cameraId = restaValida
                    ? cameraAttuale!.Id
                    : (await assegnazioneCamera.RisolviCameraLiberaAsync(strutturaId, tipologiaId, request.CheckIn, request.CheckOut, prenotazioneId, cancellationToken)).Id;
            }
            else
            {
                throw new ConflictException("Specifica una camera o una tipologia.");
            }

            await ValidaCameraECheckInOutAsync(strutturaId, cameraId, request.CheckIn, request.CheckOut, cancellationToken);
            await EnsureNessunaSovrapposizioneAsync(strutturaId, cameraId, request.CheckIn, request.CheckOut, escludiPrenotazioneId: prenotazioneId, cancellationToken);

            entity.CameraId = cameraId;
            entity.TipologiaId = request.TipologiaId;
            entity.Agenzia = request.Agenzia;
            entity.NumeroPrenotazione = await NumeroPrenotazioneOAutoIncrementoAsync(strutturaId, request.Agenzia, request.NumeroPrenotazione, request.CheckIn.Year, cancellationToken);
            entity.CheckIn = request.CheckIn;
            entity.CheckOut = request.CheckOut;
            entity.NumeroOspiti = request.NumeroOspiti;
            entity.Anno = request.CheckIn.Year;
            // Spese di pulizia/Animali/Cauzione sono solo informativi: si correggono liberamente in
            // ogni momento senza altri effetti. Tassa di soggiorno invece pilota le schedine
            // Alloggiati Web/Osservatorio/PayTourist — se il valore cambia rispetto a quello
            // attuale, le ricalcoliamo con la stessa formula della creazione: disattivata (o
            // servizio non concesso alla struttura) → marcata "già inviata" senza inviare nulla;
            // riattivata → torna "da inviare" per i soli servizi concessi. Se il toggle non cambia,
            // i 3 flag non si toccano, per non sovrascrivere lo stato di un invio realmente già
            // avvenuto nel frattempo.
            if (request.TassaSoggiornoAttiva != entity.TassaSoggiornoAttiva)
            {
                var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken)
                    ?? throw new NotFoundException("Struttura non trovata.");
                var tassaDisattivata = !request.TassaSoggiornoAttiva;
                entity.StatePolice = tassaDisattivata || !struttura.AlloggiatiWebAbilitato;
                entity.PMS = tassaDisattivata || !struttura.OsservatorioAbilitato;
                entity.PayTourist = tassaDisattivata || !struttura.PayTouristAbilitato;

                // L'importo già calcolato non ha più senso se il servizio è disattivato — azzerato
                // subito, non lasciato "congelato" al vecchio valore finché non si ritocca la scheda
                // ospiti. Riattivandolo, si ricalcola subito sulla scheda già compilata (se esiste),
                // altrimenti resta 0 finché non viene compilata/risalvata.
                if (tassaDisattivata)
                {
                    entity.TotalTax = 0;
                }
                else
                {
                    var scheda = await ospitiService.GetSchedaAsync(currentUser, strutturaId, prenotazioneId, cancellationToken);
                    entity.TotalTax = scheda is null ? 0 : await ospitiService.CalcolaTassaSoggiornoAsync(strutturaId, entity, scheda, cancellationToken);
                }
            }

            entity.TassaSoggiornoAttiva = request.TassaSoggiornoAttiva;
            entity.SpesePuliziaAttiva = request.SpesePuliziaAttiva;
            entity.AnimaliAttiva = request.AnimaliAttiva;
            entity.CauzioneAttiva = request.CauzioneAttiva;
        }

        entity.ImportoPrenotazione = request.ImportoPrenotazione;
        entity.ImportoPagato = request.ImportoPagato;
        entity.ImportoTotale = request.ImportoTotale;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await prenotazioni.UpdateAsync(entity, cancellationToken);

        if (!completata && checkInPrima is not null && checkOutPrima is not null && entity.CheckIn is not null && entity.CheckOut is not null)
        {
            var dataInizio = (checkInPrima.Value < entity.CheckIn.Value ? checkInPrima.Value : entity.CheckIn.Value).Date;
            var dataFine = (checkOutPrima.Value > entity.CheckOut.Value ? checkOutPrima.Value : entity.CheckOut.Value).Date;
            await SincronizzaDisponibilitaOtaAsync(strutturaId, dataInizio, dataFine, cancellationToken);
        }

        var modificheEconomiche = new List<string>();
        if (tassaSoggiornoPrima != entity.TassaSoggiornoAttiva)
        {
            modificheEconomiche.Add($"Tassa di soggiorno {(tassaSoggiornoPrima ? "attiva" : "disattivata")}→{(entity.TassaSoggiornoAttiva ? "attiva" : "disattivata")}");
        }
        if (spesePuliziaPrima != entity.SpesePuliziaAttiva)
        {
            modificheEconomiche.Add($"Spese di pulizia {(spesePuliziaPrima ? "attive" : "disattivate")}→{(entity.SpesePuliziaAttiva ? "attive" : "disattivate")}");
        }
        if (animaliPrima != entity.AnimaliAttiva)
        {
            modificheEconomiche.Add($"Animali {(animaliPrima ? "attivo" : "disattivato")}→{(entity.AnimaliAttiva ? "attivo" : "disattivato")}");
        }
        if (cauzionePrima != entity.CauzioneAttiva)
        {
            modificheEconomiche.Add($"Cauzione {(cauzionePrima ? "attiva" : "disattivata")}→{(entity.CauzioneAttiva ? "attiva" : "disattivata")}");
        }
        if (importoPrenotazionePrima != entity.ImportoPrenotazione)
        {
            modificheEconomiche.Add($"Importo prenotazione {importoPrenotazionePrima?.ToString("0.00") ?? "—"}€→{entity.ImportoPrenotazione?.ToString("0.00") ?? "—"}€");
        }
        if (importoTotalePrima != entity.ImportoTotale)
        {
            modificheEconomiche.Add($"Importo totale {importoTotalePrima?.ToString("0.00") ?? "—"}€→{entity.ImportoTotale?.ToString("0.00") ?? "—"}€");
        }
        if (importoPagatoPrima != entity.ImportoPagato)
        {
            modificheEconomiche.Add($"Importo pagato {importoPagatoPrima?.ToString("0.00") ?? "—"}€→{entity.ImportoPagato?.ToString("0.00") ?? "—"}€");
        }
        if (totalTaxPrima != entity.TotalTax)
        {
            modificheEconomiche.Add($"Tassa di soggiorno calcolata {totalTaxPrima?.ToString("0.00") ?? "—"}€→{entity.TotalTax?.ToString("0.00") ?? "—"}€");
        }

        var dettaglio = modificheEconomiche.Count > 0 ? $" ({string.Join("; ", modificheEconomiche)})" : string.Empty;
        await LogPrenotazioneAsync(currentUser, strutturaId, $"Prenotazione #{entity.NumeroPrenotazione ?? entity.Id.ToString()[..8]} modificata{dettaglio}.", cancellationToken);
        return entity;
    }

    /// <summary>
    /// Annullamento (soft-cancel, DeletePrenotazione del legacy): non elimina la riga, azzera gli
    /// importi e marca Annullata — nessun hard-delete di una Prenotazione.
    /// </summary>
    public async Task<Prenotazione> AnnullaAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);

        var entity = await GetOwnedAsync(strutturaId, prenotazioneId, cancellationToken);
        // La camera va liberata SOLO se era proprio questa prenotazione a tenerla occupata (check-in
        // già fatto): se era ancora Incompleta (soggiorno futuro, check-in mai avvenuto), la camera
        // non è mai stata toccata da lei — se risulta non Pronta è per un altro motivo (altro
        // soggiorno in corso, blocco manuale) e non va alterato qui, altrimenti si libera una camera
        // che è invece legittimamente occupata da qualcun altro.
        var eraInCorso = entity.StatoPrenotazione == StatoPrenotazione.InCorso;

        entity.ImportoPrenotazione = 0;
        entity.ImportoPagato = 0;
        entity.ImportoTotale = 0;
        entity.StatoPrenotazione = StatoPrenotazione.Annullata;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await prenotazioni.UpdateAsync(entity, cancellationToken);
        if (eraInCorso)
        {
            await LiberaCameraAsync(entity.CameraId, cancellationToken);
        }

        // La camera va riaperta lato OTA per l'intero periodo del soggiorno annullato, non solo se
        // era già in corso: anche annullare un soggiorno futuro libera quelle date sul pool.
        if (entity.CheckIn is not null && entity.CheckOut is not null)
        {
            await SincronizzaDisponibilitaOtaAsync(strutturaId, entity.CheckIn.Value, entity.CheckOut.Value, cancellationToken);
        }

        await LogPrenotazioneAsync(currentUser, strutturaId, $"Prenotazione #{entity.NumeroPrenotazione ?? entity.Id.ToString()[..8]} annullata.", cancellationToken);
        return entity;
    }

    /// <summary>
    /// Riporta la camera a Pronta quando la prenotazione che la occupava (In corso, check-in già
    /// fatto) viene annullata — va chiamata solo in quel caso, altrimenti rischia di liberare una
    /// camera occupata per un motivo indipendente dalla prenotazione annullata. Non tocca camere già
    /// Pronte, per non generare un UPDATE a vuoto.
    /// </summary>
    private async Task LiberaCameraAsync(Guid? cameraId, CancellationToken cancellationToken)
    {
        if (cameraId is not { } id)
        {
            return;
        }

        var camera = await camere.GetAsync(id, cancellationToken);
        if (camera is null || camera.StateRoom == StatoCamera.Pronta)
        {
            return;
        }

        camera.StateRoom = StatoCamera.Pronta;
        camera.UpdatedAtUtc = DateTime.UtcNow;
        await camere.UpdateAsync(camera, cancellationToken);
    }

    public async Task<Prenotazione> CheckInAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.RoomStatusUpdate, cancellationToken);

        var prenotazione = await GetOwnedAsync(strutturaId, prenotazioneId, cancellationToken);
        var camera = await GetCameraDellaPrenotazioneAsync(prenotazione, cancellationToken);

        if (camera.StateRoom == StatoCamera.Occupata)
        {
            throw new ConflictException("La camera è attualmente occupata, non è possibile effettuare il check-in.");
        }

        camera.StateRoom = StatoCamera.Occupata;
        camera.UpdatedAtUtc = DateTime.UtcNow;
        prenotazione.StatoPrenotazione = StatoPrenotazione.InCorso;

        // Da qui decorrono i termini di legge per la schedina alloggiati (vedi TerminiSchedina):
        // registrato solo al primo check-in, così un check-in ripetuto per errore non fa ripartire
        // un termine che nella realtà era già iniziato.
        prenotazione.CheckInEffettuatoAtUtc ??= DateTime.UtcNow;
        prenotazione.UpdatedAtUtc = DateTime.UtcNow;

        await camere.UpdateAsync(camera, cancellationToken);
        await prenotazioni.UpdateAsync(prenotazione, cancellationToken);
        return prenotazione;
    }

    /// <summary>
    /// Check-out: se il CheckOut previsto è nel futuro si tratta di un check-out anticipato (la
    /// data viene aggiornata a oggi, la Permanenza sulla scheda Ospiti viene ricalcolata di
    /// conseguenza — il legacy propagava questo ricalcolo in ChangeRoomStatus, gap segnalato nel
    /// report di Fase 3 e chiuso qui perché la Permanenza per-ospite conta per la schedina
    /// Alloggiati Web di questa Fase 6 — e la tassa di soggiorno viene rifatta sulle notti
    /// effettive invece di restare ferma a quelle pianificate al check-in, richiesta esplicita
    /// dell'utente: check-in 1→10 con checkout anticipato al 3 deve tassare 2 notti, non 9).
    /// Se la cauzione non viene restituita al cliente, viene registrato un movimento Cauzione. La
    /// camera passa sempre a DaPulire — il legacy aveva un bypass a Pronta se l'integrazione
    /// desktop "SyncMobile" non era installata, concetto che non esiste più nella versione web.
    /// </summary>
    public async Task<Prenotazione> CheckOutAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CheckOutRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.RoomStatusUpdate, cancellationToken);

        var prenotazione = await GetOwnedAsync(strutturaId, prenotazioneId, cancellationToken);
        var camera = await GetCameraDellaPrenotazioneAsync(prenotazione, cancellationToken);

        var oggi = DateTime.UtcNow.Date;
        // Checkout anticipato = le notti tra oggi e il check-out originariamente previsto si
        // liberano davvero (non sono più occupate da questo soggiorno): senza spingerlo su Wubook
        // quella camera resta "chiusa" lato OTA anche per le date ormai libere — la stessa esigenza
        // di "zero attese" già seguita per creazione/modifica/annullamento.
        var checkOutPrevisto = prenotazione.CheckOut;
        if (checkOutPrevisto is { } fine && fine.Date > oggi && prenotazione.CheckIn is { } checkIn)
        {
            prenotazione.CheckOut = oggi;
            await RicalcolaPermanenzaETassaAsync(strutturaId, prenotazione, checkIn.Date, oggi, cancellationToken);
        }

        if (!request.RestituisciCauzione && request.ImportoCauzioneTrattenuta is { } importo && importo > 0)
        {
            await cauzioni.AddAsync(
                new Cauzione
                {
                    StrutturaId = strutturaId,
                    PrenotazioneId = prenotazione.Id,
                    ImportoCauzione = importo,
                    DataInserimento = DateTime.UtcNow,
                },
                cancellationToken);
        }

        camera.StateRoom = StatoCamera.DaPulire;
        camera.UpdatedAtUtc = DateTime.UtcNow;
        prenotazione.StatoPrenotazione = StatoPrenotazione.Completata;
        prenotazione.UpdatedAtUtc = DateTime.UtcNow;

        await camere.UpdateAsync(camera, cancellationToken);
        await prenotazioni.UpdateAsync(prenotazione, cancellationToken);

        if (checkOutPrevisto is { } fineOriginale && fineOriginale.Date > oggi && prenotazione.CheckIn is { } checkInOriginale)
        {
            await SincronizzaDisponibilitaOtaAsync(strutturaId, checkInOriginale.Date, fineOriginale.Date, cancellationToken);
        }

        // Il check-out è appena stato effettuato: se un job aveva già segnalato "check-out
        // dimenticato" per questa prenotazione (vedi CheckOutDimenticatoNotificaJob), il problema è
        // risolto — la notifica va segnata letta invece di restare visibile a torto.
        await notificaService.RisolviPerPrenotazioneAsync(strutturaId, TipoNotifica.CheckOutDimenticato, prenotazione.Id, cancellationToken);
        return prenotazione;
    }

    /// <summary>Cambio stato manuale da griglia camere (terzo scenario di ChangeRoomStatus, vedi report sez. C.2).</summary>
    public async Task<SettingRoom> CambiaStatoCameraManualeAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, StatoCamera nuovoStato, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.RoomStatusUpdate, cancellationToken);

        var prenotazione = await GetOwnedAsync(strutturaId, prenotazioneId, cancellationToken);
        var camera = await GetCameraDellaPrenotazioneAsync(prenotazione, cancellationToken);

        if (camera.StateRoom == StatoCamera.Occupata && nuovoStato == StatoCamera.Occupata)
        {
            throw new ConflictException("La camera è attualmente occupata, non è possibile effettuare il check-in.");
        }

        if (nuovoStato == StatoCamera.Occupata)
        {
            prenotazione.StatoPrenotazione = StatoPrenotazione.InCorso;
        }
        else if (nuovoStato == StatoCamera.DaPulire)
        {
            prenotazione.StatoPrenotazione = StatoPrenotazione.Completata;
        }

        camera.StateRoom = nuovoStato;
        camera.UpdatedAtUtc = DateTime.UtcNow;
        prenotazione.UpdatedAtUtc = DateTime.UtcNow;

        await camere.UpdateAsync(camera, cancellationToken);
        await prenotazioni.UpdateAsync(prenotazione, cancellationToken);
        return camera;
    }

    /// <summary>
    /// Ricalcola Permanenza sulla scheda Ospiti (capofamiglia + membri) e l'importo della tassa di
    /// soggiorno dopo un check-out anticipato — entrambi erano calcolati sulle notti pianificate al
    /// check-in/salvataggio scheda, non su quelle effettivamente soggiornate.
    /// </summary>
    private async Task RicalcolaPermanenzaETassaAsync(Guid strutturaId, Prenotazione prenotazione, DateTime checkIn, DateTime nuovoCheckOut, CancellationToken cancellationToken)
    {
        var ospite = await ospiti.GetByPrenotazioneAsync(prenotazione.Id, cancellationToken);
        if (ospite is null)
        {
            return;
        }

        var permanenza = Math.Max((nuovoCheckOut - checkIn).Days, 0);
        ospite.Permanenza = permanenza;
        ospite.UpdatedAtUtc = DateTime.UtcNow;

        foreach (var membro in ospite.Membri)
        {
            membro.Permanenza = permanenza;
            membro.UpdatedAtUtc = DateTime.UtcNow;
        }

        prenotazione.TotalTax = await ospitiService.CalcolaTassaSoggiornoAsync(strutturaId, prenotazione, ospite, cancellationToken);

        await ospiti.SaveChangesAsync(cancellationToken);
    }

    private async Task<SettingRoom> GetCameraDellaPrenotazioneAsync(Prenotazione prenotazione, CancellationToken cancellationToken)
    {
        if (prenotazione.CameraId is not { } cameraId)
        {
            throw new ConflictException("La prenotazione non ha una camera associata.");
        }

        return await camere.GetAsync(cameraId, cancellationToken)
            ?? throw new NotFoundException("Camera non trovata.");
    }

    private async Task ValidaCameraECheckInOutAsync(Guid strutturaId, Guid cameraId, DateTime checkIn, DateTime checkOut, CancellationToken cancellationToken)
    {
        if (checkOut.Date <= checkIn.Date)
        {
            throw new ConflictException("La data di check-out deve essere successiva al check-in.");
        }

        var camera = await camere.GetAsync(cameraId, cancellationToken)
            ?? throw new NotFoundException("Camera non trovata.");
        if (camera.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Camera non trovata.");
        }
    }

    /// <summary>
    /// Nuovo controllo (non presente nel legacy, vedi report Fase 3 sez. D.2): impedisce di
    /// salvare due prenotazioni non annullate sovrapposte sulla stessa camera.
    /// </summary>
    private async Task EnsureNessunaSovrapposizioneAsync(
        Guid strutturaId,
        Guid cameraId,
        DateTime checkIn,
        DateTime checkOut,
        Guid? escludiPrenotazioneId,
        CancellationToken cancellationToken)
    {
        var sovrapposta = await prenotazioni.EsisteSovrapposizioneAsync(strutturaId, cameraId, checkIn, checkOut, escludiPrenotazioneId, cancellationToken);
        if (sovrapposta)
        {
            throw new ConflictException("Questa camera è già prenotata per le date selezionate.");
        }
    }

    private async Task<Prenotazione> GetOwnedAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        var entity = await prenotazioni.GetAsync(prenotazioneId, cancellationToken)
            ?? throw new NotFoundException("Prenotazione non trovata.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Prenotazione non trovata.");
        }

        return entity;
    }
}
