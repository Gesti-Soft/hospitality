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
    decimal Implemento,
    // Impostazioni OTA per l'intero pool — editabili solo dal dialog camera della pagina Servizi
    // OTA, mai dal form generale Tipologie: da passare sempre invariate per non azzerarle.
    string? CodiceCameraWubook = null,
    bool WubookSoloWoodoo = false);

public record CreaCameraRequest(
    Guid? TipologiaId,
    StatoCamera StateRoom,
    string Nome,
    int? CapacitaOspiti,
    int? SoggiornoMinimo);

/// <summary>Crea in un colpo solo una sequenza di camere numerate (es. "101".."110") della stessa Tipologia — comodo per un pool di camere reali identiche, invece di ripetere "Nuova camera" una per una.</summary>
public record CreaCamereNumerateRequest(
    Guid? TipologiaId,
    StatoCamera StateRoom,
    string? Prefisso,
    int Da,
    int A,
    int? CapacitaOspiti,
    int? SoggiornoMinimo);

public record RisultatoDuplicazioneCamere(int Tipologie, int Camere, int Prezzi, int Canali, int Saltati);

/// <summary>
/// CRUD di Tipologie camera e Camere — modulo "impostazioni camere" del legacy RoomSettingLogic,
/// portato come CRUD per-risorsa (il legacy salvava l'intera griglia camere in blocco ad ogni
/// modifica, pattern incompatibile con un'API REST, vedi report Fase 3 punto D.7).
/// </summary>
public class CamereService(
    ITipologiaCameraRepository tipologie,
    ICameraRepository camere,
    IPrezzoCameraRepository prezzi,
    ICanaleVenditaRepository canali,
    IStrutturaRepository strutture,
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
            CodiceCameraWubook = NormalizzaCodiceWubook(request.CodiceCameraWubook),
            WubookSoloWoodoo = request.WubookSoloWoodoo,
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
        entity.CodiceCameraWubook = NormalizzaCodiceWubook(request.CodiceCameraWubook);
        entity.WubookSoloWoodoo = request.WubookSoloWoodoo;
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

    /// <summary>
    /// Crea in sequenza le camere da "Prefisso+Da" a "Prefisso+A" (es. Prefisso "Camera ", Da 101, A
    /// 110 → "Camera 101".."Camera 110"), stessa Tipologia/Stato/Capacità/Soggiorno minimo per tutte.
    /// Valida TUTTI i nomi prima di crearne anche uno solo, per non lasciare a metà un pool se un
    /// nome nel mezzo dell'intervallo esiste già.
    /// </summary>
    public async Task<IReadOnlyList<SettingRoom>> CreaCamereNumerateAsync(ICurrentUser currentUser, Guid strutturaId, CreaCamereNumerateRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        if (request.A < request.Da)
        {
            throw new ConflictException("Il numero finale deve essere maggiore o uguale al numero iniziale.");
        }

        var quantita = request.A - request.Da + 1;
        if (quantita > 200)
        {
            throw new ConflictException("Troppe camere in un colpo solo (massimo 200): riduci l'intervallo.");
        }

        await EnsureTipologiaValidaAsync(strutturaId, request.TipologiaId, cancellationToken);

        var prefisso = request.Prefisso?.Trim() ?? string.Empty;
        var nomi = Enumerable.Range(request.Da, quantita).Select(n => $"{prefisso}{n}").ToList();

        foreach (var nome in nomi)
        {
            if (await camere.ExistsByNomeAsync(strutturaId, nome, escludiId: null, cancellationToken))
            {
                throw new ConflictException($"Esiste già una camera con nome \"{nome}\".");
            }
        }

        var entita = new List<SettingRoom>();
        foreach (var nome in nomi)
        {
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
            entita.Add(entity);
        }

        return entita;
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

    /// <summary>
    /// Segna una camera come pulita (torna "Pronta") — usata dalla pagina Pulizie. Permesso
    /// `RoomStatusUpdate` invece di `SettingRoomWrite`: un addetto pulizie deve poter aggiornare lo
    /// stato della camera senza avere accesso a crearne/modificarne l'anagrafica.
    /// </summary>
    public async Task<SettingRoom> SegnaPulitaAsync(ICurrentUser currentUser, Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.RoomStatusUpdate, cancellationToken);

        var entity = await GetCameraOwnedAsync(strutturaId, cameraId, cancellationToken);
        entity.StateRoom = StatoCamera.Pronta;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await camere.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    private static string? NormalizzaCodiceWubook(string? codice)
    {
        if (string.IsNullOrWhiteSpace(codice))
        {
            return null;
        }

        var alfanumerico = new string(codice.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return alfanumerico.Length == 0 ? null : (alfanumerico.Length <= 4 ? alfanumerico : alfanumerico[..4]);
    }

    public async Task EliminaCameraAsync(ICurrentUser currentUser, Guid strutturaId, Guid cameraId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        // Fedele al legacy: nessun controllo su prenotazioni collegate, la FK
        // Prenotazione.CameraId è SetNull, quindi le prenotazioni passate restano ma "orfane".
        // L'associazione OTA vive sulla Tipologia (il pool), non più sulla singola camera: eliminare
        // una camera reale riduce semplicemente da sola la quantità che verrà inviata a Wubook al
        // prossimo giro di sincronizzazione — nessuna chiamata Wubook da fare qui.
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

    // --- Duplicazione da un'altra Struttura ---

    /// <summary>
    /// Duplica tipologie/camere/prezzi/canali vendita da un'altra Struttura attiva dello stesso
    /// Cliente — pensata per una Struttura nuova che vuole ripartire dalla configurazione di una
    /// struttura gemella invece di ricrearla da zero. Le camere/tipologie il cui nome esiste già
    /// nella struttura di destinazione vengono SALTATE (non sovrascritte, non duplicate) — riportate
    /// nel conteggio "Saltati" così l'operatore sa cosa non è stato copiato; i prezzi collegati a
    /// una camera/tipologia saltata vengono saltati a loro volta (non avrebbero a chi riferirsi).
    /// L'associazione Wubook (IdCameraWubook/WubookAttiva) NON viene mai copiata: è specifica di
    /// questa struttura, non ha senso trasferirla da un'altra.
    /// </summary>
    public async Task<RisultatoDuplicazioneCamere> DuplicaDaAsync(ICurrentUser currentUser, Guid strutturaId, Guid strutturaOrigineId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        if (strutturaId == strutturaOrigineId)
        {
            throw new ConflictException("Seleziona una struttura di origine diversa da quella corrente.");
        }

        var clienteDestinazione = await strutture.GetClienteIdAsync(strutturaId, cancellationToken);
        var clienteOrigine = await strutture.GetClienteIdAsync(strutturaOrigineId, cancellationToken);
        if (clienteOrigine is null || clienteDestinazione is null || clienteOrigine != clienteDestinazione)
        {
            throw new ConflictException("Puoi duplicare solo da un'altra struttura dello stesso Cliente.");
        }

        int saltati = 0;

        var tipologieOrigine = await tipologie.ListByStrutturaAsync(strutturaOrigineId, cancellationToken);
        var mappaTipologie = new Dictionary<Guid, Guid>();
        foreach (var t in tipologieOrigine)
        {
            var nome = t.TipologiaCamera.Trim();
            if (await tipologie.ExistsByNomeAsync(strutturaId, nome, escludiId: null, cancellationToken))
            {
                saltati++;
                continue;
            }

            var nuova = new SettingTipologia
            {
                StrutturaId = strutturaId,
                TipologiaCamera = nome,
                SpesePulizia = t.SpesePulizia,
                Animali = t.Animali,
                Cauzione = t.Cauzione,
                PrezzoDefault = t.PrezzoDefault,
                NumeroImplementoPersona = t.NumeroImplementoPersona,
                Implemento = t.Implemento,
            };
            await tipologie.AddAsync(nuova, cancellationToken);
            mappaTipologie[t.Id] = nuova.Id;
        }

        var camereOrigine = await camere.ListByStrutturaAsync(strutturaOrigineId, cancellationToken);
        var mappaCamere = new Dictionary<Guid, Guid>();
        foreach (var c in camereOrigine)
        {
            var nome = c.Nome.Trim();
            if (await camere.ExistsByNomeAsync(strutturaId, nome, escludiId: null, cancellationToken))
            {
                saltati++;
                continue;
            }

            Guid? nuovaTipologiaId = null;
            if (c.TipologiaId is { } tId)
            {
                if (!mappaTipologie.TryGetValue(tId, out var mappata))
                {
                    // La tipologia di origine non è stata duplicata (nome già esistente): la camera
                    // non ha più a cosa agganciarsi in modo affidabile, saltata anche lei.
                    saltati++;
                    continue;
                }

                nuovaTipologiaId = mappata;
            }

            var nuova = new SettingRoom
            {
                StrutturaId = strutturaId,
                TipologiaId = nuovaTipologiaId,
                StateRoom = StatoCamera.Pronta,
                Nome = nome,
                CapacitaOspiti = c.CapacitaOspiti,
                SoggiornoMinimo = c.SoggiornoMinimo,
            };
            await camere.AddAsync(nuova, cancellationToken);
            mappaCamere[c.Id] = nuova.Id;
        }

        var prezziOrigine = await prezzi.ListByStrutturaAsync(strutturaOrigineId, cancellationToken);
        var prezziDuplicati = 0;
        foreach (var p in prezziOrigine)
        {
            Guid? nuovaCameraId = null, nuovaTipologiaId = null;
            if (p.CameraId is { } cId)
            {
                if (!mappaCamere.TryGetValue(cId, out var mappata))
                {
                    saltati++;
                    continue;
                }

                nuovaCameraId = mappata;
            }
            else if (p.TipologiaId is { } tId)
            {
                if (!mappaTipologie.TryGetValue(tId, out var mappata))
                {
                    saltati++;
                    continue;
                }

                nuovaTipologiaId = mappata;
            }
            else
            {
                continue;
            }

            prezzi.Add(new GestionePrezzo
            {
                StrutturaId = strutturaId,
                CameraId = nuovaCameraId,
                TipologiaId = nuovaTipologiaId,
                DataInizio = p.DataInizio,
                DataFine = p.DataFine,
                PrezzoPerNotte = p.PrezzoPerNotte,
            });
            prezziDuplicati++;
        }

        if (prezziDuplicati > 0)
        {
            await prezzi.SaveChangesAsync(cancellationToken);
        }

        var canaliOrigine = await canali.ListByStrutturaAsync(strutturaOrigineId, cancellationToken);
        var canaliDuplicati = 0;
        foreach (var c in canaliOrigine)
        {
            var descrizione = c.Descrizione.Trim();
            if (await canali.ExistsByDescrizioneAsync(strutturaId, descrizione, escludiId: null, cancellationToken))
            {
                saltati++;
                continue;
            }

            await canali.AddAsync(new SettingAgenzia { StrutturaId = strutturaId, Descrizione = descrizione }, cancellationToken);
            canaliDuplicati++;
        }

        return new RisultatoDuplicazioneCamere(mappaTipologie.Count, mappaCamere.Count, prezziDuplicati, canaliDuplicati, saltati);
    }
}
