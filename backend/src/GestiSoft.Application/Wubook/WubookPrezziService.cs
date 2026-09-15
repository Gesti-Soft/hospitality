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
    public async Task SincronizzaAsync(ICurrentUser currentUser, Guid strutturaId, DateTime dataInizio, DateTime dataFine, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        if (dataFine.Date < dataInizio.Date)
        {
            throw new ConflictException("La data di fine non può essere precedente alla data di inizio.");
        }

        var (token, lcode) = await licenzaService.GetCredenzialiValideAsync(strutturaId, cancellationToken);

        var tipologieSincronizzate = (await tipologie.ListByStrutturaAsync(strutturaId, cancellationToken))
            .Where(t => t.WubookAttiva && t.IdCameraWubook is not null)
            .ToList();

        if (tipologieSincronizzate.Count == 0)
        {
            throw new ConflictException("Nessuna tipologia sincronizzata con l'OTA: sincronizza prima le camere.");
        }

        var giorni = (dataFine.Date - dataInizio.Date).Days + 1;
        var prezziPerTipologia = new Dictionary<int, IReadOnlyList<decimal>>();

        foreach (var tipologia in tipologieSincronizzate)
        {
            // cameraId=Guid.Empty: nessuna camera reale ha questo id, quindi la query (che matcha
            // "CameraId == cameraId OR (CameraId nullo E TipologiaId == tipologiaId)") ritorna solo
            // i periodi di prezzo a livello di Tipologia — l'unico canale che ha senso pushare qui.
            var periodi = await prezzi.ListPerCalendarioAsync(strutturaId, Guid.Empty, tipologia.Id, dataInizio.Date, dataFine.Date, cancellationToken);

            var prezziGiorno = new List<decimal>(giorni);
            for (var giorno = dataInizio.Date; giorno <= dataFine.Date; giorno = giorno.AddDays(1))
            {
                var diTipologia = periodi.FirstOrDefault(p => p.CameraId == null && p.TipologiaId == tipologia.Id && Copre(p, giorno));
                prezziGiorno.Add(diTipologia?.PrezzoPerNotte ?? tipologia.PrezzoDefault ?? 0m);
            }

            prezziPerTipologia[tipologia.IdCameraWubook!.Value] = prezziGiorno;
        }

        await wubookClient.UpdatePlanPricesAsync(token, lcode, dataInizio.Date, prezziPerTipologia, cancellationToken);
    }

    private static bool Copre(GestionePrezzo periodo, DateTime giorno) =>
        periodo.DataInizio is { } inizio && periodo.DataFine is { } fine && giorno >= inizio.Date && giorno <= fine.Date;
}
