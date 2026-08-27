using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

/// <summary>
/// Push del calendario prezzi verso Wubook (update_plan_prices, piano Parity id 0) — porta
/// ComunicationLogic.SyncPricesLocalWithOta del legacy. Risolve il prezzo giorno-per-giorno con
/// la stessa priorità camera-specifica &gt; tipologia &gt; default tipologia di PrezziCameraService
/// (nessun supplemento persona qui: quello si applica solo al preventivo di una prenotazione, non
/// al listino pubblicato sui canali OTA).
/// </summary>
public class WubookPrezziService(
    ICameraRepository camere,
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

        var camereSincronizzate = (await camere.ListByStrutturaAsync(strutturaId, cancellationToken))
            .Where(c => c.WubookAttiva && c.IdCameraWubook is not null)
            .ToList();

        if (camereSincronizzate.Count == 0)
        {
            throw new ConflictException("Nessuna camera sincronizzata con Wubook: sincronizza prima le camere.");
        }

        var giorni = (dataFine.Date - dataInizio.Date).Days + 1;
        var prezziPerCamera = new Dictionary<int, IReadOnlyList<decimal>>();

        foreach (var camera in camereSincronizzate)
        {
            var tipologia = camera.TipologiaId is { } tipologiaId ? await tipologie.GetAsync(tipologiaId, cancellationToken) : null;
            var periodi = await prezzi.ListPerCalendarioAsync(strutturaId, camera.Id, camera.TipologiaId, dataInizio.Date, dataFine.Date, cancellationToken);

            var prezziGiorno = new List<decimal>(giorni);
            for (var giorno = dataInizio.Date; giorno <= dataFine.Date; giorno = giorno.AddDays(1))
            {
                var specifico = periodi.FirstOrDefault(p => p.CameraId == camera.Id && Copre(p, giorno));
                var diTipologia = specifico is null
                    ? periodi.FirstOrDefault(p => p.CameraId == null && p.TipologiaId == camera.TipologiaId && Copre(p, giorno))
                    : null;

                prezziGiorno.Add(specifico?.PrezzoPerNotte ?? diTipologia?.PrezzoPerNotte ?? tipologia?.PrezzoDefault ?? 0m);
            }

            prezziPerCamera[camera.IdCameraWubook!.Value] = prezziGiorno;
        }

        await wubookClient.UpdatePlanPricesAsync(token, lcode, dataInizio.Date, prezziPerCamera, cancellationToken);
    }

    private static bool Copre(GestionePrezzo periodo, DateTime giorno) =>
        periodo.DataInizio is { } inizio && periodo.DataFine is { } fine && giorno >= inizio.Date && giorno <= fine.Date;
}
