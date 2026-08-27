using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Camere;

public record CreaTipologiaRequest(
    string TipologiaCamera,
    decimal? SpesePulizia,
    decimal? Animali,
    decimal? Cauzione,
    decimal? PrezzoDefault,
    int NumeroImplementoPersona,
    decimal Implemento);

public record CreaCameraRequest(
    Guid? TipologiaId,
    StatoCamera StateRoom,
    string Nome,
    int? CapacitaOspiti,
    int? SoggiornoMinimo);

/// <summary>
/// CRUD di Tipologie camera e Camere — modulo "impostazioni camere" del legacy RoomSettingLogic,
/// portato come CRUD per-risorsa (il legacy salvava l'intera griglia camere in blocco ad ogni
/// modifica, pattern incompatibile con un'API REST, vedi report Fase 3 punto D.7).
/// </summary>
public class CamereService(
    ITipologiaCameraRepository tipologie,
    ICameraRepository camere,
    PermessoStrutturaGuard permessoGuard)
{
    // --- Tipologie ---

    public async Task<IReadOnlyList<SettingTipologia>> ListaTipologieAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);
        return await tipologie.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<SettingTipologia> CreaTipologiaAsync(ICurrentUser currentUser, Guid strutturaId, CreaTipologiaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var nome = request.TipologiaCamera.Trim();
        if (await tipologie.ExistsByNomeAsync(strutturaId, nome, escludiId: null, cancellationToken))
        {
            throw new ConflictException("Esiste già una tipologia con questo nome.");
        }

        var entity = new SettingTipologia
        {
            StrutturaId = strutturaId,
            TipologiaCamera = nome,
            SpesePulizia = request.SpesePulizia,
            Animali = request.Animali,
            Cauzione = request.Cauzione,
            PrezzoDefault = request.PrezzoDefault,
            NumeroImplementoPersona = request.NumeroImplementoPersona,
            Implemento = request.Implemento,
        };

        await tipologie.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<SettingTipologia> AggiornaTipologiaAsync(ICurrentUser currentUser, Guid strutturaId, Guid tipologiaId, CreaTipologiaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var entity = await GetTipologiaOwnedAsync(strutturaId, tipologiaId, cancellationToken);

        var nome = request.TipologiaCamera.Trim();
        if (await tipologie.ExistsByNomeAsync(strutturaId, nome, escludiId: tipologiaId, cancellationToken))
        {
            throw new ConflictException("Esiste già una tipologia con questo nome.");
        }

        entity.TipologiaCamera = nome;
        entity.SpesePulizia = request.SpesePulizia;
        entity.Animali = request.Animali;
        entity.Cauzione = request.Cauzione;
        entity.PrezzoDefault = request.PrezzoDefault;
        entity.NumeroImplementoPersona = request.NumeroImplementoPersona;
        entity.Implemento = request.Implemento;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await tipologie.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    public async Task EliminaTipologiaAsync(ICurrentUser currentUser, Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var entity = await GetTipologiaOwnedAsync(strutturaId, tipologiaId, cancellationToken);
        await tipologie.DeleteAsync(entity, cancellationToken);
    }

    private async Task<SettingTipologia> GetTipologiaOwnedAsync(Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        var entity = await tipologie.GetAsync(tipologiaId, cancellationToken)
            ?? throw new NotFoundException("Tipologia non trovata.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Tipologia non trovata.");
        }

        return entity;
    }

    // --- Camere ---

    public async Task<IReadOnlyList<SettingRoom>> ListaCamereAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);
        return await camere.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<SettingRoom> CreaCameraAsync(ICurrentUser currentUser, Guid strutturaId, CreaCameraRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var nome = request.Nome.Trim();
        if (await camere.ExistsByNomeAsync(strutturaId, nome, escludiId: null, cancellationToken))
        {
            throw new ConflictException("Esiste già una camera con questo nome.");
        }

        await EnsureTipologiaValidaAsync(strutturaId, request.TipologiaId, cancellationToken);

        var entity = new SettingRoom
        {
            StrutturaId = strutturaId,
            TipologiaId = request.TipologiaId,
            StateRoom = request.StateRoom,
            Nome = nome,
            CapacitaOspiti = request.CapacitaOspiti,
            SoggiornoMinimo = request.SoggiornoMinimo,
        };

        await camere.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<SettingRoom> AggiornaCameraAsync(ICurrentUser currentUser, Guid strutturaId, Guid cameraId, CreaCameraRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var entity = await GetCameraOwnedAsync(strutturaId, cameraId, cancellationToken);

        var nome = request.Nome.Trim();
        if (await camere.ExistsByNomeAsync(strutturaId, nome, escludiId: cameraId, cancellationToken))
        {
            throw new ConflictException("Esiste già una camera con questo nome.");
        }

        await EnsureTipologiaValidaAsync(strutturaId, request.TipologiaId, cancellationToken);

        entity.TipologiaId = request.TipologiaId;
        entity.StateRoom = request.StateRoom;
        entity.Nome = nome;
        entity.CapacitaOspiti = request.CapacitaOspiti;
        entity.SoggiornoMinimo = request.SoggiornoMinimo;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await camere.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    public async Task EliminaCameraAsync(ICurrentUser currentUser, Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        // Fedele al legacy: nessun controllo su prenotazioni collegate, la FK
        // Prenotazione.CameraId è SetNull, quindi le prenotazioni passate restano ma "orfane".
        var entity = await GetCameraOwnedAsync(strutturaId, cameraId, cancellationToken);
        await camere.DeleteAsync(entity, cancellationToken);
    }

    private async Task<SettingRoom> GetCameraOwnedAsync(Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        var entity = await camere.GetAsync(cameraId, cancellationToken)
            ?? throw new NotFoundException("Camera non trovata.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Camera non trovata.");
        }

        return entity;
    }

    private async Task EnsureTipologiaValidaAsync(Guid strutturaId, Guid? tipologiaId, CancellationToken cancellationToken)
    {
        if (tipologiaId is not { } id)
        {
            return;
        }

        var tipologia = await tipologie.GetAsync(id, cancellationToken);
        if (tipologia is null || tipologia.StrutturaId != strutturaId)
        {
            throw new ConflictException("La tipologia indicata non appartiene a questa struttura.");
        }
    }
}
