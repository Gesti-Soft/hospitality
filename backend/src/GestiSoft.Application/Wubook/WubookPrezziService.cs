using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

/// <summary>
/// Push del calendario prezzi verso Wubook (update_plan_prices, piano Parity id 0) — porta
/// ComunicationLogic.SyncPricesLocalWithOta del legacy. Ora per Tipologia (pool di camere
/// identiche): esiste un solo canale prezzo per pool, quindi si usa solo il prezzo a livello di
/// Tipologia (periodo dedicato o <see cref="SettingTipologia.PrezzoDefault"/>) — un eventuale
/// prezzo camera-specifico non ha più senso da pushare qui (nessuna singola camera del pool ha più
/// una propria voce OTA). Nessun supplemento persona qui: quello si applica solo al preventivo di
/// una prenotazione, non al listino pubblicato sui canali OTA.
/// </summary>
public class WubookPrezziService(
    ITipologiaCameraRepository tipologie,
    IPrezzoCameraRepository prezzi,
    IWubookClient wubookClient,
    WubookLicenzaService licenzaService,
    PermessoStrutturaGuard permessoGuard)
{
    /// <summary>WuBook rifiuta i prezzi sotto questa soglia (documentazione di update_plan_prices: "must be greater than 0.01").</summary>
    private const decimal PrezzoMinimoOta = 0.01m;

    /// <summary>Sincronizzazione manuale dalla pagina Servizi OTA. Restituisce gli avvisi sui giorni rimasti senza prezzo.</summary>
    public async Task<IReadOnlyList<string>> SincronizzaAsync(ICurrentUser currentUser, Guid strutturaId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var tipologieSincronizzate = (await tipologie.ListByStrutturaAsync(strutturaId, cancellationToken))
            .Where(t => t.WubookAttiva && t.IdCameraWubook is not null)
            .ToList();

        if (tipologieSincronizzate.Count == 0)
        {
            throw new ConflictException("Nessuna tipologia sincronizzata con l'OTA: sincronizza prima le camere.");
        }

        return await InviaAsync(strutturaId, tipologieSincronizzate, dataInizio, dataFine, cancellationToken);
    }

    /// <summary>
    /// Invio automatico dopo che un periodo di prezzo della tipologia è stato salvato o eliminato:
    /// senza, il prezzo cambiava solo nel gestionale e sui portali restava quello vecchio finché
    /// qualcuno non si ricordava di sincronizzare a mano. Null se la tipologia non è collegata
    /// all'OTA (nessun invio da fare), altrimenti gli avvisi sui giorni senza prezzo.
    /// </summary>
    public async Task<IReadOnlyList<string>?> InviaTipologiaAsync(Guid strutturaId, Guid tipologiaId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken)
    {
        var tipologia = await tipologie.GetAsync(tipologiaId, cancellationToken);
        if (tipologia is null || tipologia.StrutturaId != strutturaId || !tipologia.WubookAttiva || tipologia.IdCameraWubook is null)
        {
            return null;
        }

        return await InviaAsync(strutturaId, [tipologia], dataInizio, dataFine, cancellationToken);
    }

    /// <summary>
    /// I giorni senza un prezzo valido (nessun periodo e nessun Prezzo default) non si mandano:
    /// WuBook rifiuta i prezzi a zero, e con un solo giorno a zero rischiava di saltare l'intero
    /// invio. Si mandano i tratti consecutivi che un prezzo ce l'hanno; per gli altri giorni sul
    /// portale resta il prezzo che c'era, e l'operatore riceve un avviso con le date.
    /// </summary>
    private async Task<IReadOnlyList<string>> InviaAsync(
        Guid strutturaId, IReadOnlyList<SettingTipologia> tipologieDaInviare, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken)
    {
        if (dataFine.Date < dataInizio.Date)
        {
            throw new ConflictException("La data di fine non può essere precedente alla data di inizio.");
        }

        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);

        var avvisi = new List<string>();
        // Tratti con lo stesso inizio e la stessa lunghezza viaggiano nella stessa chiamata: senza
        // buchi, tutte le tipologie partono insieme come prima.
        var invii = new Dictionary<(DateTime Inizio, int Giorni), Dictionary<int, IReadOnlyList<decimal>>>();

        foreach (var tipologia in tipologieDaInviare)
        {
            // cameraId=Guid.Empty: nessuna camera reale ha questo id, quindi la query (che matcha
            // "CameraId == cameraId OR (CameraId nullo E TipologiaId == tipologiaId)") ritorna solo
            // i periodi di prezzo a livello di Tipologia — l'unico canale che ha senso pushare qui.
            var periodi = await prezzi.ListPerCalendarioAsync(strutturaId, Guid.Empty, tipologia.Id, dataInizio.Date, dataFine.Date, cancellationToken);

            var giorni = new List<(DateTime Giorno, decimal? Prezzo)>();
            for (var giorno = dataInizio.Date; giorno <= dataFine.Date; giorno = giorno.AddDays(1))
            {
                var diTipologia = periodi.FirstOrDefault(p => p.CameraId == null && p.TipologiaId == tipologia.Id && Copre(p, giorno));
                var prezzo = diTipologia?.PrezzoPerNotte ?? tipologia.PrezzoDefault;
                giorni.Add((giorno, prezzo >= PrezzoMinimoOta ? prezzo : null));
            }

            foreach (var tratto in Tratti(giorni))
            {
                if (tratto.Prezzi is null)
                {
                    avvisi.Add($"{tipologia.TipologiaCamera}: nessun prezzo {DescriviPeriodo(tratto.Inizio, tratto.Giorni)}, sui portali resta quello di prima. Imposta un periodo o il Prezzo default della tipologia.");
                    continue;
                }

                var chiave = (tratto.Inizio, tratto.Giorni);
                if (!invii.TryGetValue(chiave, out var perTipologia))
                {
                    invii[chiave] = perTipologia = [];
                }

                perTipologia[tipologia.IdCameraWubook!.Value] = tratto.Prezzi;
            }
        }

        foreach (var ((inizio, _), prezziPerTipologia) in invii)
        {
            await wubookClient.UpdatePlanPricesAsync(token, lcode, inizio, prezziPerTipologia, cancellationToken);
        }

        return avvisi;
    }

    /// <summary>Divide i giorni in tratti consecutivi: con un prezzo (da inviare) o senza (Prezzi null).</summary>
    public static IEnumerable<(DateTime Inizio, int Giorni, IReadOnlyList<decimal>? Prezzi)> Tratti(IReadOnlyList<(DateTime Giorno, decimal? Prezzo)> giorni)
    {
        var i = 0;
        while (i < giorni.Count)
        {
            var conPrezzo = giorni[i].Prezzo is not null;
            var j = i;
            while (j < giorni.Count && (giorni[j].Prezzo is not null) == conPrezzo)
            {
                j++;
            }

            yield return (giorni[i].Giorno, j - i, conPrezzo ? giorni.Skip(i).Take(j - i).Select(g => g.Prezzo!.Value).ToList() : null);
            i = j;
        }
    }

    private static string DescriviPeriodo(DateTime inizio, int giorni) =>
        giorni == 1 ? $"il {inizio:dd/MM/yyyy}" : $"dal {inizio:dd/MM/yyyy} al {inizio.AddDays(giorni - 1):dd/MM/yyyy}";

    private static bool Copre(GestionePrezzo periodo, DateTime giorno) =>
        periodo.DataInizio is { } inizio && periodo.DataFine is { } fine && giorno >= inizio.Date && giorno <= fine.Date;
}
