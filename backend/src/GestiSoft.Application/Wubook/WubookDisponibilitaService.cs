using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Prenotazioni;

namespace GestiSoft.Application.Wubook;

/// <summary>
/// Push di disponibilità (update_avail) e restrizioni soggiorno (rplan_update_rplan_values,
/// piano di default pid=0, fedele al legacy) verso Wubook — per ogni camera sincronizzata,
/// giorno per giorno: 0 se occupata da una prenotazione locale non annullata o coperta da una
/// <see cref="ChiusuraCamera"/> attiva, 1 altrimenti; soggiorno minimo/massimo dalla
/// <see cref="RestrizioneSoggiornoCamera"/> attiva per quel giorno se presente, altrimenti dal
/// valore fisso <see cref="SettingRoom.SoggiornoMinimo"/> della camera.
/// Fondamentale per evitare overbooking sui canali OTA quando una camera viene prenotata
/// direttamente nel gestionale (il legacy aveva lo stesso scopo, qui esplicitato come servizio
/// dedicato invece che implicito nella UI desktop). A differenza del legacy — che leggeva la
/// disponibilità corrente da Wubook (fetch_single_room) e vi sottraeva le chiusure — qui la
/// disponibilità viene ricalcolata sempre da zero dai soli dati locali (prenotazioni + chiusure),
/// stessa fonte di verità già usata per il resto della sincronizzazione: più semplice e non
/// soggetta a disallineamenti se Wubook e il gestionale divergono.
/// </summary>
public class WubookDisponibilitaService(
    ICameraRepository camere,
    IPrenotazioneRepository prenotazioni,
    IChiusuraCameraRepository chiusure,
    IRestrizioneSoggiornoCameraRepository restrizioniPeriodo,
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
        var restrizioniPerCameraWubook = new Dictionary<int, IReadOnlyList<(int? MinStay, int? MaxStay)>>();

        foreach (var camera in camereSincronizzate)
        {
            var occupazioni = await prenotazioni.ListOccupazioneAsync(strutturaId, camera.Id, dataInizio.Date, dataFine.Date, cancellationToken);
            var chiusureCamera = await chiusure.ListSovrapposteAsync(camera.Id, dataInizio.Date, dataFine.Date, cancellationToken);
            var restrizioniCamera = await restrizioniPeriodo.ListSovrapposteAsync(camera.Id, dataInizio.Date, dataFine.Date, cancellationToken);

            var disponibilitaGiorni = new List<int>(giorni);
            var restrizioniGiorni = new List<(int?, int?)>(giorni);
            for (var giorno = dataInizio.Date; giorno <= dataFine.Date; giorno = giorno.AddDays(1))
            {
                var occupata = occupazioni.Any(p => giorno >= p.CheckIn!.Value.Date && giorno < p.CheckOut!.Value.Date);
                var chiusa = chiusureCamera.Any(c => giorno >= c.DataInizio.Date && giorno <= c.DataFine.Date);
                disponibilitaGiorni.Add(occupata || chiusa ? 0 : 1);

                var restrizionePeriodo = restrizioniCamera.FirstOrDefault(r => giorno >= r.DataInizio.Date && giorno <= r.DataFine.Date);
                restrizioniGiorni.Add(restrizionePeriodo is not null
                    ? (restrizionePeriodo.MinStay, restrizionePeriodo.MaxStay)
                    : (camera.SoggiornoMinimo, null));
            }

            disponibilitaPerCamera[camera.IdCameraWubook!.Value] = disponibilitaGiorni;
            restrizioniPerCameraWubook[camera.IdCameraWubook!.Value] = restrizioniGiorni;
        }

        await wubookClient.UpdateAvailabilityAsync(token, lcode, dataInizio.Date, disponibilitaPerCamera, cancellationToken);
        await wubookClient.UpdateRestrizioniAsync(token, lcode, dataInizio.Date, restrizioniPerCameraWubook, cancellationToken);
    }
}
