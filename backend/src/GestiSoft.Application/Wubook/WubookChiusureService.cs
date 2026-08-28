using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public record CreaChiusuraCameraRequest(Guid CameraId, DateTime DataInizio, DateTime DataFine, string? Motivo, int? Quantita);

/// <summary>Riga della lista "chiusure" — manuale (Id valorizzato, eliminabile) oppure derivata al volo da una prenotazione reale (Id null, mai eliminabile, non un record persistito).</summary>
public record BloccoCamera(Guid? Id, DateTime DataInizio, DateTime DataFine, string? Motivo, int? Quantita, string Origine);

/// <summary>
/// Chiusure camera per periodo (manutenzione, ecc.) — porta RoomsController.closures del legacy.
/// CRUD locale puro: il push verso Wubook (update_avail) avviene sempre tramite l'azione esplicita
/// "Sincronizza disponibilità" già esistente (WubookDisponibilitaService), stesso pattern già usato
/// per camere/prezzi/disponibilità in questo modulo — non un'azione implicita qui.
/// </summary>
public class WubookChiusureService(
    IChiusuraCameraRepository chiusure,
    ICameraRepository camere,
    IPrenotazioneRepository prenotazioni,
    PermessoStrutturaGuard permessoGuard)
{
    /// <summary>
    /// Chiusure manuali + prenotazioni reali correnti nella stessa lista, sola lettura per queste
    /// ultime — a differenza del legacy (che materializza una riga "Prenotato: Nome" per ogni
    /// prenotazione, stato duplicato da tenere sincronizzato), qui le prenotazioni si leggono al
    /// volo da Prenotazione (fonte unica, mai disallineata se una prenotazione viene annullata).
    /// Finestra fissa ±365gg da oggi: oltre non è operativamente rilevante per questa vista.
    /// </summary>
    public async Task<IReadOnlyList<BloccoCamera>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);
        await CameraOwnedAsync(strutturaId, cameraId, cancellationToken);

        var manuali = await chiusure.ListByCameraAsync(cameraId, cancellationToken);
        var oggi = DateTime.UtcNow.Date;
        var prenotate = await prenotazioni.ListOccupazioneAsync(strutturaId, cameraId, oggi.AddDays(-365), oggi.AddDays(365), cancellationToken);

        var risultato = manuali
            .Select(c => new BloccoCamera(c.Id, c.DataInizio, c.DataFine, c.Motivo, c.Quantita, "Manuale"))
            .Concat(prenotate.Select(p => new BloccoCamera(
                null,
                p.CheckIn ?? oggi,
                p.CheckOut ?? oggi,
                p.Ospite is { } o ? $"Prenotato: {o.Nome} {o.Cognome}".Trim() : "Prenotazione",
                1,
                "Prenotazione")))
            .OrderBy(b => b.DataInizio)
            .ToList();

        return risultato;
    }

    public async Task<ChiusuraCamera> CreaAsync(ICurrentUser currentUser, Guid strutturaId, CreaChiusuraCameraRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);
        await CameraOwnedAsync(strutturaId, request.CameraId, cancellationToken);

        if (request.DataFine.Date < request.DataInizio.Date)
        {
            throw new ConflictException("La data di fine non può essere precedente alla data di inizio.");
        }

        var entity = new ChiusuraCamera
        {
            StrutturaId = strutturaId,
            CameraId = request.CameraId,
            DataInizio = request.DataInizio.Date,
            DataFine = request.DataFine.Date,
            Motivo = request.Motivo,
            Quantita = request.Quantita,
        };
        await chiusure.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task EliminaAsync(ICurrentUser currentUser, Guid strutturaId, Guid chiusuraId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var entity = await chiusure.GetAsync(chiusuraId, cancellationToken) ?? throw new NotFoundException("Chiusura non trovata.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Chiusura non trovata.");
        }

        await chiusure.DeleteAsync(entity, cancellationToken);
    }

    private async Task CameraOwnedAsync(Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        var camera = await camere.GetAsync(cameraId, cancellationToken) ?? throw new NotFoundException("Camera non trovata.");
        if (camera.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Camera non trovata.");
        }
    }
}
