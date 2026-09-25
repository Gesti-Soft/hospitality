using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Contracts.Camere;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Camere;

public record ImpostaPrezzoRequest(Guid? CameraId, Guid? TipologiaId, DateTime DataInizio, DateTime DataFine, decimal PrezzoPerNotte);

public record PreventivoResult(int Notti, decimal Totale);

/// <summary>
/// Calendario prezzi — porta CalendarLogic del legacy: AddOrUpdatePrice (algoritmo di split per
/// intervalli sovrapposti) e GetImport (calcolo preventivo giorno-per-giorno con risoluzione
/// camera-specifica &gt; tipologia &gt; default tipologia, vedi report Fase 3 sez. C.6).
/// </summary>
public class PrezziCameraService(
    IPrezzoCameraRepository prezzi,
    ICameraRepository camere,
    ITipologiaCameraRepository tipologie,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<GestionePrezzo>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);
        return await prezzi.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<GestionePrezzo> ImpostaPrezzoAsync(ICurrentUser currentUser, Guid strutturaId, ImpostaPrezzoRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        if (request.DataInizio.Date > request.DataFine.Date)
        {
            throw new ConflictException("La data di inizio non può essere successiva alla data di fine.");
        }

        var haCamera = request.CameraId is not null;
        var haTipologia = request.TipologiaId is not null;
        if (haCamera == haTipologia)
        {
            throw new ConflictException("Specificare esattamente uno tra camera e tipologia per il prezzo.");
        }

        if (haCamera)
        {
            var camera = await camere.GetAsync(request.CameraId!.Value, cancellationToken)
                ?? throw new NotFoundException("Camera non trovata.");
            if (camera.StrutturaId != strutturaId)
            {
                throw new NotFoundException("Camera non trovata.");
            }
        }
        else
        {
            var tipologia = await tipologie.GetAsync(request.TipologiaId!.Value, cancellationToken)
                ?? throw new NotFoundException("Tipologia non trovata.");
            if (tipologia.StrutturaId != strutturaId)
            {
                throw new NotFoundException("Tipologia non trovata.");
            }
        }

        var dataInizio = request.DataInizio.Date;
        var dataFine = request.DataFine.Date;

        var sovrapposti = await prezzi.ListSovrappostiAsync(strutturaId, request.CameraId, request.TipologiaId, dataInizio, dataFine, cancellationToken);

        var frammenti = new List<GestionePrezzo>();
        foreach (var esistente in sovrapposti)
        {
            var inizioEsistente = esistente.DataInizio!.Value.Date;
            var fineEsistente = esistente.DataFine!.Value.Date;

            if (inizioEsistente < dataInizio)
            {
                frammenti.Add(NuovoFrammento(strutturaId, esistente, inizioEsistente, dataInizio.AddDays(-1)));
            }

            if (fineEsistente > dataFine)
            {
                frammenti.Add(NuovoFrammento(strutturaId, esistente, dataFine.AddDays(1), fineEsistente));
            }

            prezzi.Remove(esistente);
        }

        // RemoveDuplicates del legacy: due frammenti identici possono generarsi solo se due
        // periodi esistenti si toccavano esattamente al bordo del nuovo intervallo.
        var frammentiUnici = frammenti
            .GroupBy(f => (f.CameraId, f.TipologiaId, f.PrezzoPerNotte, f.DataInizio, f.DataFine))
            .Select(g => g.First());

        foreach (var frammento in frammentiUnici)
        {
            prezzi.Add(frammento);
        }

        var nuovo = new GestionePrezzo
        {
            StrutturaId = strutturaId,
            CameraId = request.CameraId,
            TipologiaId = request.TipologiaId,
            DataInizio = dataInizio,
            DataFine = dataFine,
            PrezzoPerNotte = request.PrezzoPerNotte,
        };
        prezzi.Add(nuovo);

        await prezzi.SaveChangesAsync(cancellationToken);
        return nuovo;
    }

    public async Task EliminaAsync(ICurrentUser currentUser, Guid strutturaId, Guid prezzoId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var entity = await prezzi.GetAsync(prezzoId, cancellationToken)
            ?? throw new NotFoundException("Periodo prezzo non trovato.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Periodo prezzo non trovato.");
        }

        prezzi.Remove(entity);
        await prezzi.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Come Booking: al massimo tre fasce, così le stesse regole si possono riportare uguali sul portale.</summary>
    public const int FasceEtaMassime = 3;

    public async Task<IReadOnlyList<FasciaEtaSupplemento>> ListaFasceEtaAsync(ICurrentUser currentUser, Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead, cancellationToken);
        await GetTipologiaAsync(strutturaId, tipologiaId, cancellationToken);
        return await tipologie.ListFasceEtaAsync(strutturaId, tipologiaId, cancellationToken);
    }

    /// <summary>
    /// Sostituisce le fasce della tipologia. Su un endpoint a parte, come le frequenze delle
    /// pulizie: la pagina OTA rimanda il form della tipologia con un elenco fisso di campi, e un
    /// campo nuovo lì verrebbe azzerato a ogni suo salvataggio.
    /// </summary>
    public async Task<IReadOnlyList<FasciaEtaSupplemento>> SalvaFasceEtaAsync(
        ICurrentUser currentUser, Guid strutturaId, Guid tipologiaId, IReadOnlyList<FasciaEtaSupplementoDto> richieste, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);
        await GetTipologiaAsync(strutturaId, tipologiaId, cancellationToken);

        ValidaFasceEta(richieste);

        var fasce = richieste
            .OrderBy(f => f.EtaMin)
            .Select(f => new FasciaEtaSupplemento
            {
                StrutturaId = strutturaId,
                TipologiaId = tipologiaId,
                EtaMin = f.EtaMin,
                EtaMax = f.EtaMax,
                ImportoPerNotte = f.ImportoPerNotte,
            })
            .ToList();

        await tipologie.SostituisciFasceEtaAsync(strutturaId, tipologiaId, fasce, cancellationToken);
        return fasce;
    }

    public static void ValidaFasceEta(IReadOnlyList<FasciaEtaSupplementoDto> fasce)
    {
        if (fasce.Count > FasceEtaMassime)
        {
            throw new ConflictException($"Si possono impostare al massimo {FasceEtaMassime} fasce d'età.");
        }

        foreach (var f in fasce)
        {
            if (f.EtaMin < 0 || f.EtaMax > 17 || f.EtaMin > f.EtaMax)
            {
                throw new ConflictException("Le fasce d'età vanno da 0 a 17 anni, con l'età iniziale non superiore a quella finale: dai 18 anni si paga il supplemento pieno.");
            }

            if (f.ImportoPerNotte < 0)
            {
                throw new ConflictException("L'importo di una fascia non può essere negativo: 0 vuol dire gratis.");
            }
        }

        var ordinate = fasce.OrderBy(f => f.EtaMin).ToList();
        for (var i = 1; i < ordinate.Count; i++)
        {
            if (ordinate[i].EtaMin <= ordinate[i - 1].EtaMax)
            {
                throw new ConflictException($"Le fasce {ordinate[i - 1].EtaMin}–{ordinate[i - 1].EtaMax} e {ordinate[i].EtaMin}–{ordinate[i].EtaMax} anni si sovrappongono: ogni età deve stare in una fascia sola.");
            }
        }
    }

    private async Task<SettingTipologia> GetTipologiaAsync(Guid strutturaId, Guid tipologiaId, CancellationToken cancellationToken)
    {
        var tipologia = await tipologie.GetAsync(tipologiaId, cancellationToken);
        if (tipologia is null || tipologia.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Tipologia non trovata.");
        }

        return tipologia;
    }

    /// <summary>
    /// Calcolo preventivo giorno-per-giorno (GetImport del legacy): per ogni notte del
    /// soggiorno risolve il prezzo con priorità camera-specifica &gt; tipologia &gt; default
    /// tipologia, e somma il supplemento per-persona (giornaliero, non una tantum) se il numero
    /// ospiti eccede la soglia della tipologia. Il supplemento Animali (se il toggle è attivo) è
    /// anch'esso giornaliero — si somma una volta per notte, non una tantum. Spese di
    /// pulizia/Cauzione restano invece extra fissi indipendenti dalla durata del soggiorno.
    /// </summary>
    public async Task<PreventivoResult> CalcolaPreventivoAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        Guid cameraId,
        DateTime checkIn,
        DateTime checkOut,
        int numeroOspiti,
        IReadOnlyList<int> etaBambini,
        bool spesePuliziaAttiva,
        bool animaliAttiva,
        bool cauzioneAttiva,
        CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);

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

        var tipologia = camera.TipologiaId is { } tipologiaId
            ? await tipologie.GetAsync(tipologiaId, cancellationToken)
            : null;

        var periodi = await prezzi.ListPerCalendarioAsync(strutturaId, cameraId, camera.TipologiaId, checkIn.Date, checkOut.Date.AddDays(-1), cancellationToken);

        var fasce = tipologia is null ? [] : await tipologie.ListFasceEtaAsync(strutturaId, tipologia.Id, cancellationToken);
        var supplementoPerNotte = tipologia is null ? 0m : SupplementoPerNotte(tipologia, fasce, numeroOspiti, etaBambini);

        decimal totale = 0;
        var notti = 0;
        for (var giorno = checkIn.Date; giorno < checkOut.Date; giorno = giorno.AddDays(1))
        {
            var prezzoCamera = periodi.FirstOrDefault(p => p.CameraId == cameraId && Copre(p, giorno));
            var prezzoTipologia = prezzoCamera is null
                ? periodi.FirstOrDefault(p => p.CameraId == null && p.TipologiaId == camera.TipologiaId && Copre(p, giorno))
                : null;

            totale += prezzoCamera?.PrezzoPerNotte ?? prezzoTipologia?.PrezzoPerNotte ?? tipologia?.PrezzoDefault ?? 0m;

            totale += supplementoPerNotte;

            if (tipologia is not null && animaliAttiva)
            {
                totale += tipologia.Animali ?? 0m;
            }

            notti++;
        }

        if (tipologia is not null)
        {
            if (spesePuliziaAttiva) totale += tipologia.SpesePulizia ?? 0m;
            if (cauzioneAttiva) totale += tipologia.Cauzione ?? 0m;
        }

        return new PreventivoResult(notti, totale);
    }

    /// <summary>
    /// Supplemento di una notte per gli ospiti oltre quelli inclusi nel prezzo. I posti inclusi
    /// vanno ai più grandi, adulti per primi: la tariffa ridotta resta così ai bambini, invece di
    /// far pagare il supplemento pieno a un adulto perché un bambino ha preso il suo posto.
    /// Ogni ospite in più paga la fascia della sua età, oppure il supplemento pieno se è adulto o
    /// se la sua età non rientra in nessuna fascia (come fa Booking). Senza fasce il risultato è
    /// quello di sempre: supplemento pieno per ogni ospite in più.
    /// </summary>
    public static decimal SupplementoPerNotte(SettingTipologia tipologia, IReadOnlyList<FasciaEtaSupplemento> fasce, int numeroOspiti, IReadOnlyList<int> etaBambini)
    {
        var inPiu = numeroOspiti - tipologia.NumeroImplementoPersona;
        if (inPiu <= 0)
        {
            return 0m;
        }

        // I bambini sono compresi negli ospiti: se le età sono più degli ospiti (la scheda ospiti
        // ha abbassato il numero dopo), si tengono le più piccole, che sono quelle che restano fuori
        // dai posti inclusi.
        var bambini = etaBambini.OrderBy(e => e).Take(numeroOspiti).ToList();
        var adulti = numeroOspiti - bambini.Count;

        // Dal più grande al più piccolo: null è un adulto. Gli ultimi "inPiu" sono quelli che pagano.
        var ospiti = Enumerable.Repeat<int?>(null, adulti)
            .Concat(bambini.OrderByDescending(e => e).Select(e => (int?)e));

        return ospiti.Skip(tipologia.NumeroImplementoPersona).Sum(eta =>
            eta is { } anni && fasce.FirstOrDefault(f => anni >= f.EtaMin && anni <= f.EtaMax) is { } fascia
                ? fascia.ImportoPerNotte
                : tipologia.Implemento);
    }

    private static bool Copre(GestionePrezzo periodo, DateTime giorno) =>
        periodo.DataInizio is { } inizio && periodo.DataFine is { } fine && giorno >= inizio.Date && giorno <= fine.Date;

    private static GestionePrezzo NuovoFrammento(Guid strutturaId, GestionePrezzo origine, DateTime dataInizio, DateTime dataFine) => new()
    {
        StrutturaId = strutturaId,
        CameraId = origine.CameraId,
        TipologiaId = origine.TipologiaId,
        DataInizio = dataInizio,
        DataFine = dataFine,
        PrezzoPerNotte = origine.PrezzoPerNotte,
    };
}
