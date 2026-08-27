using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Prenotazioni;

namespace GestiSoft.Application.Wubook;

/// <summary>
/// Push di disponibilità (update_avail) e restrizioni soggiorno (rplan_update_rplan_values,
/// piano di default pid=0, fedele al legacy) verso Wubook — per ogni camera sincronizzata,
/// giorno per giorno: 0 se occupata da una prenotazione locale non annullata, 1 altrimenti.
/// Fondamentale per evitare overbooking sui canali OTA quando una camera viene prenotata
/// direttamente nel gestionale (il legacy aveva lo stesso scopo, qui esplicitato come servizio
/// dedicato invece che implicito nella UI desktop).
/// </summary>
public class WubookDisponibilitaService(
    ICameraRepository camere,
    IPrenotazioneRepository prenotazioni,
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
        var disponibilitaPerCamera = new Dictionary<int, IReadOnlyList<int>>();
        var restrizioniPerCamera = new Dictionary<int, IReadOnlyList<(int? MinStay, int? MaxStay)>>();

        foreach (var camera in camereSincronizzate)
        {
            var occupazioni = await prenotazioni.ListOccupazioneAsync(strutturaId, camera.Id, dataInizio.Date, dataFine.Date, cancellationToken);

            var disponibilitaGiorni = new List<int>(giorni);
            var restrizioniGiorni = new List<(int?, int?)>(giorni);
            for (var giorno = dataInizio.Date; giorno <= dataFine.Date; giorno = giorno.AddDays(1))
            {
                var occupata = occupazioni.Any(p => giorno >= p.CheckIn!.Value.Date && giorno < p.CheckOut!.Value.Date);
                disponibilitaGiorni.Add(occupata ? 0 : 1);
                restrizioniGiorni.Add((camera.SoggiornoMinimo, null));
            }

            disponibilitaPerCamera[camera.IdCameraWubook!.Value] = disponibilitaGiorni;
            restrizioniPerCamera[camera.IdCameraWubook!.Value] = restrizioniGiorni;
        }

        await wubookClient.UpdateAvailabilityAsync(token, lcode, dataInizio.Date, disponibilitaPerCamera, cancellationToken);
        await wubookClient.UpdateRestrizioniAsync(token, lcode, dataInizio.Date, restrizioniPerCamera, cancellationToken);
    }
}
