using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Contracts.Pulizie;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Pulizie;

/// <summary>
/// Pulizia e cambio biancheria durante il soggiorno. La frequenza si decide dal più specifico al più
/// generale: rinuncia dell'ospite sulla prenotazione, poi tipologia, poi struttura. Chi pulisce
/// (permesso "Stato camera") vede le camere occupate e segna i servizi fatti; frequenze e rinunce le
/// imposta chi gestisce camere e prenotazioni.
/// </summary>
public class PulizieSoggiornoService(
    IPrenotazioneRepository prenotazioni,
    ITipologiaCameraRepository tipologie,
    IImpostazioniStrutturaRepository impostazioni,
    PermessoStrutturaGuard permessoGuard,
    ILogEventoService logEventi)
{
    public const int IntervalloMassimoGiorni = 30;

    private static readonly TimeZoneInfo FusoItaliano = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    /// <summary>Data civile italiana di oggi: tra mezzanotte e l'una la data UTC è ancora quella di ieri.</summary>
    public static DateTime Oggi() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoItaliano).Date;

    public record Soggiorno(Prenotazione Prenotazione, string? TipologiaNome, EsitoServizioSoggiorno Pulizia, EsitoServizioSoggiorno Biancheria);

    public async Task<IReadOnlyList<Soggiorno>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.RoomStatusUpdate, cancellationToken);
        return await CalcolaAsync(strutturaId, Oggi(), cancellationToken);
    }

    public record RiepilogoGiorno(int Partenze, int Pulizie, int CambiBiancheria)
    {
        public bool Vuoto => Partenze == 0 && Pulizie == 0 && CambiBiancheria == 0;
    }

    /// <summary>
    /// Quanto lavoro c'è in un giorno, per la notifica del giorno prima e del giorno stesso (job del
    /// Worker, nessun utente): partenze da rifare, e pulizie o cambi biancheria nelle camere che
    /// restano occupate anche quella notte. Una pulizia in ritardo oggi conta anche per domani.
    /// </summary>
    public async Task<RiepilogoGiorno> RiepilogoAsync(Guid strutturaId, DateTime giorno, CancellationToken cancellationToken)
    {
        var partenze = await prenotazioni.ContaPartenzeAsync(strutturaId, giorno, cancellationToken);
        var fermate = (await CalcolaAsync(strutturaId, giorno, cancellationToken))
            .Where(s => s.Prenotazione.CheckOut is { } p && p.Date > giorno.Date)
            .ToList();

        static bool DaFare(EsitoServizioSoggiorno e) => e.Stato is StatoServizioSoggiorno.DaFareOggi or StatoServizioSoggiorno.InRitardo;

        return new RiepilogoGiorno(partenze, fermate.Count(s => DaFare(s.Pulizia)), fermate.Count(s => DaFare(s.Biancheria)));
    }

    private async Task<IReadOnlyList<Soggiorno>> CalcolaAsync(Guid strutturaId, DateTime oggi, CancellationToken cancellationToken)
    {
        var inCorso = await prenotazioni.ListInCorsoAsync(strutturaId, cancellationToken);
        var tipologiePerId = (await tipologie.ListByStrutturaAsync(strutturaId, cancellationToken)).ToDictionary(t => t.Id);
        var regola = await impostazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken);

        return inCorso
            .OrderBy(p => p.Camera?.Nome)
            .Select(p =>
            {
                // Assegnata a una camera vale la tipologia della camera; in modalità pool quella della prenotazione.
                var tipologiaId = p.Camera?.TipologiaId ?? p.TipologiaId;
                var tipologia = tipologiaId is { } id && tipologiePerId.TryGetValue(id, out var t) ? t : p.Tipologia;

                var pulizia = CalcoloServizioSoggiorno.Calcola(
                    p.CheckIn, p.CheckOut, p.UltimaPuliziaSoggiorno,
                    CalcoloServizioSoggiorno.IntervalloEffettivo(tipologia?.IntervalloPuliziaGiorni, regola?.IntervalloPuliziaGiorni),
                    p.RinunciaPulizia, oggi);
                var biancheria = CalcoloServizioSoggiorno.Calcola(
                    p.CheckIn, p.CheckOut, p.UltimoCambioBiancheria,
                    CalcoloServizioSoggiorno.IntervalloEffettivo(tipologia?.IntervalloBiancheriaGiorni, regola?.IntervalloBiancheriaGiorni),
                    p.RinunciaBiancheria, oggi);

                return new Soggiorno(p, tipologia?.TipologiaCamera, pulizia, biancheria);
            })
            .ToList();
    }

    public async Task SegnaFattoAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, ServizioSoggiorno servizio, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.RoomStatusUpdate, cancellationToken);

        var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken);
        if (prenotazione is null || prenotazione.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Prenotazione non trovata.");
        }

        if (prenotazione.StatoPrenotazione != StatoPrenotazione.InCorso)
        {
            throw new ConflictException("Il soggiorno non è in corso: la pulizia di fine soggiorno si segna tra le camere da pulire.");
        }

        // Data civile salvata come le date del soggiorno: mezzanotte, senza conversioni di fuso.
        var oggi = DateTime.SpecifyKind(Oggi(), DateTimeKind.Utc);
        if (servizio == ServizioSoggiorno.Biancheria)
        {
            prenotazione.UltimoCambioBiancheria = oggi;
        }
        else
        {
            prenotazione.UltimaPuliziaSoggiorno = oggi;
        }

        prenotazione.UpdatedAtUtc = DateTime.UtcNow;
        await prenotazioni.UpdateAsync(prenotazione, cancellationToken);
    }

    /// <summary>
    /// La rinuncia la registra chi gestisce la prenotazione, perché l'ospite la comunica al banco. Nel
    /// log restano chi l'ha registrata e quando: se l'ospite poi contesta la pulizia mancata, la
    /// traccia c'è.
    /// </summary>
    public async Task<Prenotazione> AggiornaRinunceAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, RinunceServiziRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);

        var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken);
        if (prenotazione is null || prenotazione.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Prenotazione non trovata.");
        }

        var modifiche = new List<string>();
        if (prenotazione.RinunciaPulizia != request.RinunciaPulizia)
        {
            modifiche.Add(request.RinunciaPulizia ? "l'ospite rinuncia alla pulizia" : "revocata la rinuncia alla pulizia");
        }

        if (prenotazione.RinunciaBiancheria != request.RinunciaBiancheria)
        {
            modifiche.Add(request.RinunciaBiancheria ? "l'ospite rinuncia al cambio biancheria" : "revocata la rinuncia al cambio biancheria");
        }

        if (modifiche.Count == 0)
        {
            return prenotazione;
        }

        prenotazione.RinunciaPulizia = request.RinunciaPulizia;
        prenotazione.RinunciaBiancheria = request.RinunciaBiancheria;
        prenotazione.UpdatedAtUtc = DateTime.UtcNow;
        await prenotazioni.UpdateAsync(prenotazione, cancellationToken);

        await logEventi.RegistraAsync(
            LivelloLog.Info,
            $"Prenotazione {prenotazione.NumeroPrenotazione ?? prenotazione.Id.ToString()}: {string.Join(", ", modifiche)}.",
            origine: "Api",
            clienteId: currentUser.ClienteId,
            strutturaId: strutturaId,
            categoria: "Prenotazioni",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

        return prenotazione;
    }

    public async Task<SettingTipologia> AggiornaTipologiaAsync(ICurrentUser currentUser, Guid strutturaId, Guid tipologiaId, ImpostazioniPulizieTipologiaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomWrite, cancellationToken);

        var tipologia = await tipologie.GetAsync(tipologiaId, cancellationToken);
        if (tipologia is null || tipologia.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Tipologia non trovata.");
        }

        // Sulla tipologia lo 0 ha un significato suo: "nessuna", anche se la struttura la prevede.
        tipologia.IntervalloPuliziaGiorni = ValidaIntervallo(request.IntervalloPuliziaGiorni, ammettiZero: true);
        tipologia.IntervalloBiancheriaGiorni = ValidaIntervallo(request.IntervalloBiancheriaGiorni, ammettiZero: true);
        tipologia.UpdatedAtUtc = DateTime.UtcNow;
        await tipologie.UpdateAsync(tipologia, cancellationToken);
        return tipologia;
    }

    /// <summary>Usato anche dalle impostazioni della struttura, dove lo 0 non serve: lì vuoto vuol già dire nessuna.</summary>
    public static int? ValidaIntervallo(int? giorni, bool ammettiZero)
    {
        if (giorni is null)
        {
            return null;
        }

        if (giorni < 0 || giorni > IntervalloMassimoGiorni || (giorni == 0 && !ammettiZero))
        {
            throw new ConflictException(ammettiZero
                ? $"L'intervallo va da 1 a {IntervalloMassimoGiorni} giorni: 0 per nessuna, vuoto per seguire la struttura."
                : $"L'intervallo va da 1 a {IntervalloMassimoGiorni} giorni: vuoto per nessuna pulizia durante il soggiorno.");
        }

        return giorni;
    }
}
