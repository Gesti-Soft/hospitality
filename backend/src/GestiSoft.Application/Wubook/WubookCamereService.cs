using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

public record TipologiaWubookInfo(
    Guid TipologiaId,
    string TipologiaNome,
    int CamereCollegate,
    int ChiusureCount,
    int RestrizioniCount,
    int? IdCameraWubook,
    bool WubookAttiva);

/// <summary>
/// Push del pool di camere di una Tipologia verso Wubook (new_room/mod_room/del_room) — porta
/// RoomsController del legacy, ma con l'associazione OTA sulla Tipologia invece che sulla singola
/// camera: una Tipologia rappresenta N camere reali identiche, la quantità inviata a Wubook è sempre
/// il conteggio live di quelle camere (mai un numero digitato a mano), come richiesto da un cliente
/// reale con più unità identiche gestite come un unico pool.
/// </summary>
public class WubookCamereService(
    ITipologiaCameraRepository tipologie,
    ICameraRepository camere,
    IChiusuraCameraRepository chiusure,
    IRestrizioneSoggiornoCameraRepository restrizioniPeriodo,
    IWubookClient wubookClient,
    WubookLicenzaService licenzaService,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<SettingTipologia> SincronizzaAsync(ICurrentUser currentUser, Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var tipologia = await GetTipologiaOwnedAsync(strutturaId, tipologiaId, cancellationToken);
        var camereDelPool = await camere.ListByTipologiaAsync(strutturaId, tipologiaId, cancellationToken);
        if (camereDelPool.Count == 0)
        {
            throw new ConflictException("Nessuna camera reale collegata a questa tipologia: creane almeno una prima di sincronizzarla con OTA.");
        }

        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);

        var request = new WubookNuovaCameraRequest(
            Nome: tipologia.TipologiaCamera,
            ShortName: tipologia.CodiceCameraWubook ?? ShortNameDa(tipologia.TipologiaCamera),
            Occupancy: camereDelPool.Max(c => c.CapacitaOspiti) ?? 2,
            PrezzoBase: tipologia.PrezzoDefault ?? 0,
            // Mai un numero digitato a mano: sempre il conteggio reale delle camere del pool in
            // questo istante — è esattamente il bug segnalato dal vivo che questo modello risolve.
            Disponibilita: camereDelPool.Count,
            Board: "nb",
            Woodoo: tipologia.WubookSoloWoodoo);

        // Non basta guardare IdCameraWubook: dopo una rimozione resta valorizzato come storico
        // (v. RimuoviAsync) ma quella room su Wubook non esiste più — un mod_room su un id ormai
        // cancellato viene rifiutato da Wubook. Se l'associazione non è più attiva va sempre creata
        // una room nuova, indipendentemente dallo storico.
        if (!tipologia.WubookAttiva || tipologia.IdCameraWubook is not { } idEsistente)
        {
            var nuovoId = await wubookClient.NewRoomAsync(token, lcode, request, cancellationToken);
            tipologia.IdCameraWubook = nuovoId;
        }
        else
        {
            await wubookClient.ModRoomAsync(token, lcode, idEsistente, request, cancellationToken);
        }

        tipologia.WubookAttiva = true;
        tipologia.UpdatedAtUtc = DateTime.UtcNow;
        await tipologie.UpdateAsync(tipologia, cancellationToken);
        return tipologia;
    }

    public async Task<SettingTipologia> RimuoviAsync(ICurrentUser currentUser, Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var tipologia = await GetTipologiaOwnedAsync(strutturaId, tipologiaId, cancellationToken);

        if (tipologia.IdCameraWubook is { } idCameraWubook)
        {
            var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
            await wubookClient.DelRoomAsync(token, lcode, idCameraWubook, cancellationToken);
        }

        // IdCameraWubook resta valorizzato come storico (fedele al legacy: CamereAssociate.Active=false, non delete).
        tipologia.WubookAttiva = false;
        tipologia.UpdatedAtUtc = DateTime.UtcNow;
        await tipologie.UpdateAsync(tipologia, cancellationToken);
        return tipologia;
    }

    /// <summary>
    /// Elimina da OTA un pool che non ha (più) alcuna Tipologia locale associata — a differenza di
    /// RimuoviAsync, che opera sulla SettingTipologia, qui l'unico dato è l'id OTA stesso. Cerca
    /// comunque, per difesa, una Tipologia che punti ancora a questo id (caso raro di disallineamento)
    /// e la disassocia per non lasciare un puntatore verso una room ormai cancellata.
    /// </summary>
    public async Task RimuoviRemotoAsync(ICurrentUser currentUser, Guid strutturaId, int idCameraWubook, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        await wubookClient.DelRoomAsync(token, lcode, idCameraWubook, cancellationToken);

        var tipologiaOrfana = await tipologie.GetByIdWubookAsync(strutturaId, idCameraWubook, cancellationToken);
        if (tipologiaOrfana is not null)
        {
            tipologiaOrfana.WubookAttiva = false;
            tipologiaOrfana.UpdatedAtUtc = DateTime.UtcNow;
            await tipologie.UpdateAsync(tipologiaOrfana, cancellationToken);
        }
    }

    /// <summary>
    /// Elenco Tipologie della struttura con conteggio camere reali collegate, chiusure/restrizioni
    /// locali configurate e stato associazione OTA — per la tab "Camere" della pagina Servizi OTA.
    /// La disponibilità vera e propria (quella che conta per sapere se una camera è libera oggi)
    /// arriva invece da Wubook stesso, vedi <see cref="ListaCamereRemoteAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<TipologiaWubookInfo>> ListaPerAssociazioneAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);

        var lista = await tipologie.ListByStrutturaAsync(strutturaId, cancellationToken);

        var risultato = new List<TipologiaWubookInfo>();
        foreach (var t in lista)
        {
            var camereDelPool = await camere.ListByTipologiaAsync(strutturaId, t.Id, cancellationToken);
            var chiusureCount = 0;
            var restrizioniCount = 0;
            foreach (var c in camereDelPool)
            {
                chiusureCount += (await chiusure.ListByCameraAsync(c.Id, cancellationToken)).Count;
                restrizioniCount += (await restrizioniPeriodo.ListByCameraAsync(c.Id, cancellationToken)).Count;
            }

            risultato.Add(new TipologiaWubookInfo(t.Id, t.TipologiaCamera, camereDelPool.Count, chiusureCount, restrizioniCount, t.IdCameraWubook, t.WubookAttiva));
        }

        return risultato;
    }

    /// <summary>
    /// Camere già presenti su Wubook (fetch_rooms) — elenco da cui l'operatore sceglie l'associazione
    /// manuale, invece del push automatico di SincronizzaAsync. Il campo "avail" di fetch_rooms è
    /// statico e spesso inattendibile (visto dal vivo restare a 0 anche su camere realmente libere):
    /// qui viene sovrascritto con il valore reale di oggi letto da fetch_rooms_values, il
    /// contro-pezzo in lettura di update_avail (quello usato da "Sincronizza disponibilità"). Se
    /// quella seconda chiamata fallisse, si torna comunque all'elenco con l'"avail" grezzo invece di
    /// far fallire l'intera tab.
    /// </summary>
    public async Task<IReadOnlyList<WubookCamera>> ListaCamereRemoteAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);

        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);
        var camereRemote = await wubookClient.FetchRoomsAsync(token, lcode, cancellationToken);
        if (camereRemote.Count == 0)
        {
            return camereRemote;
        }

        try
        {
            var oggi = DateTime.UtcNow.Date;
            var disponibilitaOggi = await wubookClient.FetchDisponibilitaAsync(token, lcode, oggi, oggi, camereRemote.Select(c => c.Id).ToList(), cancellationToken);
            return camereRemote
                .Select(c => disponibilitaOggi.TryGetValue(c.Id, out var giorni) && giorni.Count > 0 ? c with { Disponibilita = giorni[0].Avail } : c)
                .ToList();
        }
        catch (ConflictException)
        {
            return camereRemote;
        }
    }

    /// <summary>
    /// Associazione manuale tipologia-locale ↔ camera-Wubook già esistente (pull, come
    /// otaservice.web): a differenza di SincronizzaAsync (push, crea/aggiorna una room su Wubook a
    /// partire dalla tipologia), qui NON si chiama alcuna API Wubook — si limita a salvare
    /// l'associazione scelta dall'operatore. Passare idCameraWubook=null rimuove l'associazione senza
    /// toccare Wubook (per correggere un abbinamento sbagliato).
    /// </summary>
    public async Task<SettingTipologia> AssociaAsync(ICurrentUser currentUser, Guid strutturaId, Guid tipologiaId, int? idCameraWubook, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var tipologia = await GetTipologiaOwnedAsync(strutturaId, tipologiaId, cancellationToken);

        tipologia.IdCameraWubook = idCameraWubook;
        tipologia.WubookAttiva = idCameraWubook is not null;
        tipologia.UpdatedAtUtc = DateTime.UtcNow;
        await tipologie.UpdateAsync(tipologia, cancellationToken);
        return tipologia;
    }

    private async Task<SettingTipologia> GetTipologiaOwnedAsync(Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        var tipologia = await tipologie.GetAsync(tipologiaId, cancellationToken) ?? throw new NotFoundException("Tipologia non trovata.");
        if (tipologia.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Tipologia non trovata.");
        }

        return tipologia;
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
