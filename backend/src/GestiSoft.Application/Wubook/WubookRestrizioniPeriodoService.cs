using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public record CreaRestrizioneSoggiornoCameraRequest(Guid CameraId, DateTime DataInizio, DateTime DataFine, int? MinStay, int? MaxStay, string? Motivo);

/// <summary>
/// Soggiorno minimo/massimo per camera valido solo in un periodo — porta
/// RoomsController.stay-restrictions del legacy. CRUD locale puro, stesso principio di
/// <see cref="WubookChiusureService"/>: il push (rplan_update_rplan_values) avviene tramite
/// l'azione esplicita "Sincronizza disponibilità" già esistente.
/// </summary>
public class WubookRestrizioniPeriodoService(IRestrizioneSoggiornoCameraRepository restrizioni, ICameraRepository camere, PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<RestrizioneSoggiornoCamera>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);
        await CameraOwnedAsync(strutturaId, cameraId, cancellationToken);
        return await restrizioni.ListByCameraAsync(cameraId, cancellationToken);
    }

    public async Task<RestrizioneSoggiornoCamera> CreaAsync(ICurrentUser currentUser, Guid strutturaId, CreaRestrizioneSoggiornoCameraRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);
        await CameraOwnedAsync(strutturaId, request.CameraId, cancellationToken);

        if (request.DataFine.Date < request.DataInizio.Date)
        {
            throw new ConflictException("La data di fine non può essere precedente alla data di inizio.");
        }

        if (request.MinStay is null && request.MaxStay is null)
        {
            throw new ConflictException("Specificare almeno uno tra soggiorno minimo e massimo.");
        }

        var entity = new RestrizioneSoggiornoCamera
        {
            StrutturaId = strutturaId,
            CameraId = request.CameraId,
            DataInizio = request.DataInizio.Date,
            DataFine = request.DataFine.Date,
            MinStay = request.MinStay,
            MaxStay = request.MaxStay,
            Motivo = request.Motivo,
        };
        await restrizioni.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task EliminaAsync(ICurrentUser currentUser, Guid strutturaId, Guid restrizioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var entity = await restrizioni.GetAsync(restrizioneId, cancellationToken) ?? throw new NotFoundException("Restrizione non trovata.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Restrizione non trovata.");
        }

        await restrizioni.DeleteAsync(entity, cancellationToken);
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
