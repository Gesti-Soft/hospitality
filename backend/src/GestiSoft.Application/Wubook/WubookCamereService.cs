using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

/// <summary>Push delle camere locali verso Wubook (new_room/mod_room/del_room) — porta RoomsController del legacy.</summary>
public class WubookCamereService(
    ICameraRepository camere,
    ITipologiaCameraRepository tipologie,
    IWubookClient wubookClient,
    WubookLicenzaService licenzaService,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<SettingRoom> SincronizzaAsync(ICurrentUser currentUser, Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var camera = await camere.GetAsync(cameraId, cancellationToken) ?? throw new NotFoundException("Camera non trovata.");
        if (camera.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Camera non trovata.");
        }

        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);

        var tipologia = camera.TipologiaId is { } tipologiaId ? await tipologie.GetAsync(tipologiaId, cancellationToken) : null;
        var request = new WubookNuovaCameraRequest(
            Nome: camera.Nome,
            ShortName: ShortNameDa(camera.Nome),
            Occupancy: camera.CapacitaOspiti ?? 2,
            PrezzoBase: tipologia?.PrezzoDefault ?? 0,
            Disponibilita: 1,
            Board: "nb");

        if (camera.IdCameraWubook is not { } idEsistente)
        {
            var nuovoId = await wubookClient.NewRoomAsync(token, lcode, request, cancellationToken);
            camera.IdCameraWubook = nuovoId;
        }
        else
        {
            await wubookClient.ModRoomAsync(token, lcode, idEsistente, request, cancellationToken);
        }

        camera.WubookAttiva = true;
        camera.UpdatedAtUtc = DateTime.UtcNow;
        await camere.UpdateAsync(camera, cancellationToken);
        return camera;
    }

    public async Task<SettingRoom> RimuoviAsync(ICurrentUser currentUser, Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var camera = await camere.GetAsync(cameraId, cancellationToken) ?? throw new NotFoundException("Camera non trovata.");
        if (camera.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Camera non trovata.");
        }

        if (camera.IdCameraWubook is { } idCameraWubook)
        {
            var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
            await wubookClient.DelRoomAsync(token, lcode, idCameraWubook, cancellationToken);
        }

        // IdCameraWubook resta valorizzato come storico (fedele al legacy: CamereAssociate.Active=false, non delete).
        camera.WubookAttiva = false;
        camera.UpdatedAtUtc = DateTime.UtcNow;
        await camere.UpdateAsync(camera, cancellationToken);
        return camera;
    }

    private static string ShortNameDa(string nome)
    {
        var alfanumerico = new string(nome.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (alfanumerico.Length == 0)
        {
            return "ROOM";
        }

        return alfanumerico.Length <= 4 ? alfanumerico.PadRight(4, 'X') : alfanumerico[..4];
    }
}
