using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Ospiti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Prenotazioni;

public record CreaPrenotazioneRequest(
    Guid CameraId,
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
    Guid CameraId,
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
    OspitiService ospitiService)
{
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

    public async Task<Prenotazione> GetAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        return await GetOwnedAsync(strutturaId, prenotazioneId, cancellationToken);
    }

    public async Task<Prenotazione> CreaAsync(ICurrentUser currentUser, Guid strutturaId, CreaPrenotazioneRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);

        await ValidaCameraECheckInOutAsync(strutturaId, request.CameraId, request.CheckIn, request.CheckOut, cancellationToken);
        await EnsureNessunaSovrapposizioneAsync(strutturaId, request.CameraId, request.CheckIn, request.CheckOut, escludiPrenotazioneId: null, cancellationToken);

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
            CameraId = request.CameraId,
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
        await LogPrenotazioneAsync(currentUser, strutturaId, $"Prenotazione creata (camera {request.CameraId}, {request.CheckIn:dd/MM/yyyy}–{request.CheckOut:dd/MM/yyyy}).", cancellationToken);
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

        // Snapshot "prima" dei soli campi con un impatto economico diretto — servono a registrare
        // nel log un vecchio→nuovo esplicito (non solo "modificata"), perché un operatore potrebbe
        // togliere una spunta o abbassare l'importo e incassare la differenza in nero: senza il
        // dettaglio, il log non lo renderebbe rintracciabile.
        var tassaSoggiornoPrima = entity.TassaSoggiornoAttiva;
        var spesePuliziaPrima = entity.SpesePuliziaAttiva;
        var animaliPrima = entity.AnimaliAttiva;
        var cauzionePrima = entity.CauzioneAttiva;
        var importoTotalePrima = entity.ImportoTotale;
        var importoPagatoPrima = entity.ImportoPagato;
        var totalTaxPrima = entity.TotalTax;

        if (!completata)
        {
            await ValidaCameraECheckInOutAsync(strutturaId, request.CameraId, request.CheckIn, request.CheckOut, cancellationToken);
            await EnsureNessunaSovrapposizioneAsync(strutturaId, request.CameraId, request.CheckIn, request.CheckOut, escludiPrenotazioneId: prenotazioneId, cancellationToken);

            entity.CameraId = request.CameraId;
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

        entity.ImportoPagato = request.ImportoPagato;
        entity.ImportoTotale = request.ImportoTotale;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await prenotazioni.UpdateAsync(entity, cancellationToken);

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
        if (prenotazione.CheckOut is { } checkOutPrevisto && checkOutPrevisto.Date > oggi && prenotazione.CheckIn is { } checkIn)
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
