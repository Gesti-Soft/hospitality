using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public record CreaChiusuraCameraRequest(Guid CameraId, DateTime DataInizio, DateTime DataFine, string? Motivo, int? Quantita);

/// <summary>
/// Chiusure camera per periodo (manutenzione, ecc.) — porta RoomsController.closures del legacy.
/// CRUD locale puro: il push verso Wubook (update_avail) avviene sempre tramite l'azione esplicita
/// "Sincronizza disponibilità" già esistente (WubookDisponibilitaService), stesso pattern già usato
/// per camere/prezzi/disponibilità in questo modulo — non un'azione implicita qui.
/// </summary>
public class WubookChiusureService(IChiusuraCameraRepository chiusure, ICameraRepository camere, PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<ChiusuraCamera>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);
        await CameraOwnedAsync(strutturaId, cameraId, cancellationToken);
        return await chiusure.ListByCameraAsync(cameraId, cancellationToken);
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
