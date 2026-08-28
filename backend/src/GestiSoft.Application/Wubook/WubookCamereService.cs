using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public record CameraWubookInfo(
    Guid CameraId,
    string CameraNome,
    Guid? TipologiaId,
    string? TipologiaNome,
    int? IdCameraWubook,
    bool WubookAttiva,
    bool ChiusaOggi,
    int ChiusureCount,
    int RestrizioniCount);

/// <summary>Push delle camere locali verso Wubook (new_room/mod_room/del_room) — porta RoomsController del legacy.</summary>
public class WubookCamereService(
    ICameraRepository camere,
    ITipologiaCameraRepository tipologie,
    IChiusuraCameraRepository chiusure,
    IRestrizioneSoggiornoCameraRepository restrizioniPeriodo,
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
            ShortName: camera.CodiceCameraWubook ?? ShortNameDa(camera.Nome),
            Occupancy: camera.CapacitaOspiti ?? 2,
            PrezzoBase: camera.PrezzoWubookOverride ?? tipologia?.PrezzoDefault ?? 0,
            Disponibilita: 1,
            Board: "nb",
            Woodoo: camera.WubookSoloWoodoo);

        // Non basta guardare IdCameraWubook: dopo una rimozione resta valorizzato come storico
        // (v. RimuoviAsync) ma quella room su Wubook non esiste più — un mod_room su un id ormai
        // cancellato viene rifiutato da Wubook. Se l'associazione non è più attiva va sempre creata
        // una room nuova, indipendentemente dallo storico.
        if (!camera.WubookAttiva || camera.IdCameraWubook is not { } idEsistente)
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

    /// <summary>
    /// Elenco camere locali con stato associazione Wubook e disponibilità odierna (chiusa/aperta) —
    /// per la tab "Camere" ridisegnata: select Tipologia → camere associate di quella tipologia,
    /// come otaservice.web (il legacy), invece della tabella piatta di prima. La disponibilità qui
    /// resta 0/1 per singola camera (il modello attuale è ancora 1 camera fisica = 1 camera Wubook,
    /// il pooling per Tipologia è un refactor distinto e più grosso, non affrontato qui).
    /// </summary>
    public async Task<IReadOnlyList<CameraWubookInfo>> ListaPerAssociazioneAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);

        var lista = await camere.ListByStrutturaAsync(strutturaId, cancellationToken);
        var oggi = DateTime.UtcNow.Date;

        var risultato = new List<CameraWubookInfo>();
        foreach (var c in lista)
        {
            var chiusureOggi = await chiusure.ListSovrapposteAsync(c.Id, oggi, oggi.AddDays(1), cancellationToken);
            var tutteChiusure = await chiusure.ListByCameraAsync(c.Id, cancellationToken);
            var tutteRestrizioni = await restrizioniPeriodo.ListByCameraAsync(c.Id, cancellationToken);
            risultato.Add(new CameraWubookInfo(
                c.Id, c.Nome, c.TipologiaId, c.Tipologia?.TipologiaCamera, c.IdCameraWubook, c.WubookAttiva,
                ChiusaOggi: chiusureOggi.Count > 0,
                ChiusureCount: tutteChiusure.Count,
                RestrizioniCount: tutteRestrizioni.Count));
        }

        return risultato;
    }

    /// <summary>Camere già presenti su Wubook (fetch_rooms) — elenco da cui l'operatore sceglie l'associazione manuale, invece del push automatico di SincronizzaAsync.</summary>
    public async Task<IReadOnlyList<WubookCamera>> ListaCamereRemoteAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);

        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        return await wubookClient.FetchRoomsAsync(token, lcode, cancellationToken);
    }

    /// <summary>
    /// Associazione manuale camera-locale ↔ camera-Wubook già esistente (pull, come otaservice.web):
    /// a differenza di SincronizzaAsync (push, crea/aggiorna una room su Wubook a partire dalla
    /// camera locale), qui NON si chiama alcuna API Wubook — si limita a salvare l'associazione
    /// scelta dall'operatore, fedele al legacy CamereAssociate. Passare idCameraWubook=null rimuove
    /// l'associazione senza toccare Wubook (per correggere un abbinamento sbagliato).
    /// </summary>
    public async Task<SettingRoom> AssociaAsync(ICurrentUser currentUser, Guid strutturaId, Guid cameraId, int? idCameraWubook, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var camera = await camere.GetAsync(cameraId, cancellationToken) ?? throw new NotFoundException("Camera non trovata.");
        if (camera.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Camera non trovata.");
        }

        camera.IdCameraWubook = idCameraWubook;
        camera.WubookAttiva = idCameraWubook is not null;
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
