using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Wubook;

/// <summary>
/// Push di disponibilità (update_avail) e restrizioni soggiorno (rplan_update_rplan_values,
/// piano di default pid=0, fedele al legacy) verso Wubook — per ogni Tipologia (pool di N camere
/// reali identiche) sincronizzata, giorno per giorno: quantità libera = numero di camere del pool
/// meno quelle occupate da una prenotazione locale non annullata meno quelle coperte da una
/// <see cref="ChiusuraCamera"/> attiva quel giorno (mai negativo). Soggiorno minimo/massimo preso
/// da una camera rappresentativa del pool (sono unità identiche, i valori dovrebbero coincidere) —
/// dalla <see cref="RestrizioneSoggiornoCamera"/> attiva per quel giorno se presente, altrimenti dal
/// valore fisso <see cref="SettingRoom.SoggiornoMinimo"/> di quella camera.
/// Fondamentale per evitare overbooking sui canali OTA quando una camera del pool viene prenotata
/// direttamente nel gestionale. A differenza del legacy — che leggeva la disponibilità corrente da
/// Wubook (fetch_single_room) e vi sottraeva le chiusure — qui la disponibilità viene ricalcolata
/// sempre da zero dai soli dati locali (camere + prenotazioni + chiusure), stessa fonte di verità
/// già usata per il resto della sincronizzazione: più semplice e non soggetta a disallineamenti se
/// Wubook e il gestionale divergono.
/// </summary>
public class WubookDisponibilitaService(
    ITipologiaCameraRepository tipologie,
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

        var tipologieSincronizzate = (await tipologie.ListByStrutturaAsync(strutturaId, cancellationToken))
            .Where(t => t.WubookAttiva && t.IdCameraWubook is not null)
            .ToList();

        if (tipologieSincronizzate.Count == 0)
        {
            throw new ConflictException("Nessuna tipologia sincronizzata con Wubook: sincronizza prima le camere.");
        }

        var giorni = (dataFine.Date - dataInizio.Date).Days + 1;
        var disponibilitaPerTipologia = new Dictionary<int, IReadOnlyList<int>>();
        var restrizioniPerTipologiaWubook = new Dictionary<int, IReadOnlyList<(int? MinStay, int? MaxStay)>>();

        foreach (var tipologia in tipologieSincronizzate)
        {
            var camereDelPool = await camere.ListByTipologiaAsync(strutturaId, tipologia.Id, cancellationToken);
            if (camereDelPool.Count == 0)
            {
                continue;
            }

            var cameraRappresentativa = camereDelPool[0];

            var occupazioniPerCamera = new Dictionary<Guid, IReadOnlyList<Prenotazione>>();
            var chiusurePerCamera = new Dictionary<Guid, IReadOnlyList<ChiusuraCamera>>();
            foreach (var c in camereDelPool)
            {
                occupazioniPerCamera[c.Id] = await prenotazioni.ListOccupazioneAsync(strutturaId, c.Id, dataInizio.Date, dataFine.Date, cancellationToken);
                chiusurePerCamera[c.Id] = await chiusure.ListSovrapposteAsync(c.Id, dataInizio.Date, dataFine.Date, cancellationToken);
            }

            var restrizioniRappresentativa = await restrizioniPeriodo.ListSovrapposteAsync(cameraRappresentativa.Id, dataInizio.Date, dataFine.Date, cancellationToken);

            var disponibilitaGiorni = new List<int>(giorni);
            var restrizioniGiorni = new List<(int?, int?)>(giorni);
            for (var giorno = dataInizio.Date; giorno <= dataFine.Date; giorno = giorno.AddDays(1))
            {
                var occupate = camereDelPool.Count(c =>
                    occupazioniPerCamera[c.Id].Any(p => giorno >= p.CheckIn!.Value.Date && giorno < p.CheckOut!.Value.Date));
                var chiuse = camereDelPool.Count(c =>
                    chiusurePerCamera[c.Id].Any(ch => giorno >= ch.DataInizio.Date && giorno <= ch.DataFine.Date));
                var libere = camereDelPool.Count - occupate - chiuse;
                disponibilitaGiorni.Add(Math.Max(0, libere));

                var restrizionePeriodo = restrizioniRappresentativa.FirstOrDefault(r => giorno >= r.DataInizio.Date && giorno <= r.DataFine.Date);
                restrizioniGiorni.Add(restrizionePeriodo is not null
                    ? (restrizionePeriodo.MinStay, restrizionePeriodo.MaxStay)
                    : (cameraRappresentativa.SoggiornoMinimo, null));
            }

            disponibilitaPerTipologia[tipologia.IdCameraWubook!.Value] = disponibilitaGiorni;
            restrizioniPerTipologiaWubook[tipologia.IdCameraWubook!.Value] = restrizioniGiorni;
        }

        await wubookClient.UpdateAvailabilityAsync(token, lcode, dataInizio.Date, disponibilitaPerTipologia, cancellationToken);
        await wubookClient.UpdateRestrizioniAsync(token, lcode, dataInizio.Date, restrizioniPerTipologiaWubook, cancellationToken);
    }
}
