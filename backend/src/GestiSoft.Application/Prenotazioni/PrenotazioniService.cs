using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
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
    int? NumeroOspiti);

public record AggiornaPrenotazioneRequest(
    Guid CameraId,
    string? Agenzia,
    string? NumeroPrenotazione,
    decimal? ImportoPrenotazione,
    decimal? ImportoPagato,
    decimal? ImportoTotale,
    DateTime CheckIn,
    DateTime CheckOut,
    int? NumeroOspiti);

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
    PermessoStrutturaGuard permessoGuard)
{
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

    public async Task<IReadOnlyList<Prenotazione>> ListaStoricoAsync(ICurrentUser currentUser, Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        return await prenotazioni.ListStoricoAsync(strutturaId, anno, cancellationToken);
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

        var numeroPrenotazione = request.NumeroPrenotazione;
        if (string.IsNullOrWhiteSpace(numeroPrenotazione) && string.Equals(request.Agenzia, "Diretta", StringComparison.OrdinalIgnoreCase))
        {
            var conteggio = await prenotazioni.ContaDireteAnnoAsync(strutturaId, request.CheckIn.Year, cancellationToken);
            numeroPrenotazione = (conteggio + 1).ToString();
        }

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
        };

        await prenotazioni.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<Prenotazione> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, AggiornaPrenotazioneRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);

        var entity = await GetOwnedAsync(strutturaId, prenotazioneId, cancellationToken);

        await ValidaCameraECheckInOutAsync(strutturaId, request.CameraId, request.CheckIn, request.CheckOut, cancellationToken);
        await EnsureNessunaSovrapposizioneAsync(strutturaId, request.CameraId, request.CheckIn, request.CheckOut, escludiPrenotazioneId: prenotazioneId, cancellationToken);

        entity.CameraId = request.CameraId;
        entity.Agenzia = request.Agenzia;
        entity.NumeroPrenotazione = request.NumeroPrenotazione;
        entity.ImportoPrenotazione = request.ImportoPrenotazione;
        entity.ImportoPagato = request.ImportoPagato;
        entity.ImportoTotale = request.ImportoTotale;
        entity.CheckIn = request.CheckIn;
        entity.CheckOut = request.CheckOut;
        entity.NumeroOspiti = request.NumeroOspiti;
        entity.Anno = request.CheckIn.Year;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await prenotazioni.UpdateAsync(entity, cancellationToken);
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
        entity.ImportoPrenotazione = 0;
        entity.ImportoPagato = 0;
        entity.ImportoTotale = 0;
        entity.StatoPrenotazione = StatoPrenotazione.Annullata;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await prenotazioni.UpdateAsync(entity, cancellationToken);
        return entity;
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
    /// data viene aggiornata a oggi, e la Permanenza sulla scheda Ospiti viene ricalcolata di
    /// conseguenza — il legacy propagava questo ricalcolo in ChangeRoomStatus, gap segnalato nel
    /// report di Fase 3 e chiuso qui perché la Permanenza per-ospite conta per la schedina
    /// Alloggiati Web di questa Fase 6). Se la cauzione non viene restituita al cliente, viene
    /// registrato un movimento Cauzione. La camera passa sempre a DaPulire — il legacy aveva un
    /// bypass a Pronta se l'integrazione desktop "SyncMobile" non era installata, concetto che
    /// non esiste più nella versione web.
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
            await RicalcolaPermanenzaAsync(prenotazione.Id, checkIn.Date, oggi, cancellationToken);
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

    /// <summary>Ricalcola Permanenza sulla scheda Ospiti (capofamiglia + membri) dopo un check-out anticipato.</summary>
    private async Task RicalcolaPermanenzaAsync(Guid prenotazioneId, DateTime checkIn, DateTime nuovoCheckOut, CancellationToken cancellationToken)
    {
        var ospite = await ospiti.GetByPrenotazioneAsync(prenotazioneId, cancellationToken);
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
